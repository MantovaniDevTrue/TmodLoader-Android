#!/usr/bin/env python3
"""Exercise the pinned FACTCue_Destroy C body with isolated lifecycle dependencies."""
from pathlib import Path
import argparse
import subprocess
import tempfile
import os

FIXTURE = r'''
#include <assert.h>
#include <stdint.h>
#include <stdlib.h>
typedef void* FAudioMutex;
typedef struct FACTCue FACTCue;
typedef struct { FAudioMutex apiLock; void (*pFree)(void*); } Engine;
typedef struct { Engine* parentEngine; FACTCue* cueList; } Bank;
typedef struct { FACTCue* cue; } Resource;
struct FACTCue { Bank* parentBank; FACTCue* next; Resource* simpleWave; Resource* playingSound; void* variableValues; int state; };
static int waves, sounds, notices, freed;
#define FACT_FLAG_STOP_IMMEDIATE 1
#define FACTNOTIFICATIONTYPE_CUEDESTROYED 1
#define FAUDIO_OK 0
#define FAudio_assert assert
static void FAudio_PlatformLockMutex(FAudioMutex m) { (void)m; }
static void FAudio_PlatformUnlockMutex(FAudioMutex m) { (void)m; }
static void release(void* p) { if (p) { freed++; free(p); } }
static void FACTWave_Destroy(Resource* p) { waves++; free(p); }
static void FACT_INTERNAL_DestroySound(Resource* p) { sounds++; p->cue->playingSound = NULL; free(p); }
static void FACTCue_Stop(FACTCue* p, int flags) {
    (void)flags;
    if (p->state) return;
    if (p->simpleWave) { FACTWave_Destroy(p->simpleWave); p->simpleWave = NULL; }
    if (p->playingSound) FACT_INTERNAL_DestroySound(p->playingSound);
    p->state = 1;
}
static void FACT_INTERNAL_SendCueNotification(FACTCue* p, int kind) { (void)p; (void)kind; notices++; }
'''
MAIN = r'''
int main(void) {
    Engine engine = {NULL, release};
    Bank bank = {&engine, NULL};
    for (int active = 0; active < 2; active++) for (int sound = 0; sound < 2; sound++) {
        waves = sounds = notices = freed = 0;
        FACTCue* cue = calloc(1, sizeof(FACTCue));
        cue->parentBank = &bank; cue->state = !active; cue->variableValues = malloc(8);
        Resource* resource = malloc(sizeof(Resource)); resource->cue = cue;
        if (sound) cue->playingSound = resource; else cue->simpleWave = resource;
        bank.cueList = cue;
        assert(FACTCue_Destroy(cue) == FAUDIO_OK);
        if (waves + sounds != 1) return 2;
        assert(notices == 1 && freed == 2 && bank.cueList == NULL);
    }
    assert(FACTCue_Destroy(NULL) == 1);
    return 0;
}
'''

def run(source):
    start = source.index('uint32_t FACTCue_Destroy(FACTCue *pCue)')
    end = source.index('uint32_t FACTCue_Play(', start)
    with tempfile.TemporaryDirectory() as directory:
        root = Path(directory)
        (root/'fixture.c').write_text(FIXTURE + source[start:end] + MAIN)
        subprocess.run(['cc','-std=c11','-Wall','-Wextra','-Werror','-fsanitize=address,undefined',str(root/'fixture.c'),'-o',str(root/'fixture')],check=True)
        result = subprocess.run([str(root/'fixture')],capture_output=True, env={**os.environ, "ASAN_OPTIONS":"detect_leaks=0"})
        if result.returncode not in (0, 2): print(result.stderr.decode(errors="replace"))
        return result.returncode

if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('faudio',type=Path); args = parser.parse_args()
    text = (args.faudio/'src/FACT.c').read_text()
    patch = (Path(__file__).resolve().parents[1]/'integration/native/fact_cue_cleanup.inc').read_text()
    if patch not in text: raise SystemExit('Missing cue cleanup patch')
    if run(text) != 0: raise SystemExit('Patched cue lifecycle failed')
    if run(text.replace(patch, '', 1)) == 0: raise SystemExit('Fixture did not reproduce stopped-cue resource leak')
    print('PASS FACT cue destruction: stopped/prepared and active resources, exactly-once cleanup, null handle; original stopped-cue leak reproduced')
