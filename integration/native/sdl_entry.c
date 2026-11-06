#include <jni.h>
#include <SDL3/SDL.h>
#include <android/log.h>

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
