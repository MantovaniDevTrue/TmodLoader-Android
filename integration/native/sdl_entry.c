#include <jni.h>
#include <SDL3/SDL.h>
#include <android/log.h>
#include <errno.h>
#include <fcntl.h>
#include <stdio.h>
#include <unistd.h>

__attribute__((visibility("default"))) void tml_host_flush_instructions(void *start, size_t length)
{
    __builtin___clear_cache((char *)start, (char *)start + length);
}

/* Capture libc/Mono stderr without replacing the runtime's signal handlers. */
__attribute__((visibility("default"))) int tml_host_capture_output(const char *path)
{
    int file = open(path, O_WRONLY | O_CREAT | O_TRUNC | O_CLOEXEC, 0600);
    if (file < 0) return errno;
    if (dup2(file, STDOUT_FILENO) < 0 || dup2(file, STDERR_FILENO) < 0) {
        int error = errno;
        close(file);
        return error;
    }
    close(file);
    setvbuf(stdout, NULL, _IONBF, 0);
    setvbuf(stderr, NULL, _IONBF, 0);
    fputs("HOST_NATIVE_CAPTURE_READY\n", stderr);
    return 0;
}

__attribute__((visibility("default"))) int SDL_main(int argc, char **argv)
{
    (void)argc;
    (void)argv;
    JNIEnv *env = (JNIEnv *)SDL_GetAndroidJNIEnv();
    if (!env) return 1;
    jclass entry = (*env)->FindClass(env, "com/mantovani/tmlmobile/GameEntry");
    if (!entry) goto failed;
    jmethodID run = (*env)->GetStaticMethodID(env, entry, "runGame", "()I");
    if (!run) goto failed;
    int result = (*env)->CallStaticIntMethod(env, entry, run);
    if ((*env)->ExceptionCheck(env)) goto failed;
    (*env)->DeleteLocalRef(env, entry);
    return result;
failed:
    __android_log_print(ANDROID_LOG_ERROR, "TmlHost", "SDL managed entry callback failed");
    if ((*env)->ExceptionCheck(env)) {
        (*env)->ExceptionDescribe(env);
        (*env)->ExceptionClear(env);
    }
    if (entry) (*env)->DeleteLocalRef(env, entry);
    return 1;
}
