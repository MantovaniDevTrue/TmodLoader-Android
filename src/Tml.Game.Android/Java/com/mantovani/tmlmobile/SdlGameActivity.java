package com.mantovani.tmlmobile;

import android.os.Bundle;
import org.libsdl.app.SDLActivity;

public final class SdlGameActivity extends SDLActivity {
    private static void invoke(String name, Class<?>[] types, Object... args) {
        try {
            Class.forName("com.mantovani.tmlmobile.GameEntry").getMethod(name, types).invoke(null, args);
        } catch (ReflectiveOperationException error) {
            throw new IllegalStateException("Managed SDL callback failed: " + name, error);
        }
    }
    @Override protected String[] getLibraries() {
        return new String[] { "SDL3", "FNA3D", "FAudio", "tmlhost" };
    }
    @Override protected void onCreate(Bundle state) {
        super.onCreate(state);
        invoke("attach", new Class<?>[] { android.app.Activity.class }, this);
    }
    @Override protected void onPause() {
        invoke("suspendInput", new Class<?>[0]);
        super.onPause();
    }
    @Override protected void onResume() {
        super.onResume();
        invoke("resumeInput", new Class<?>[0]);
    }
    @Override public void onWindowFocusChanged(boolean focused) {
        super.onWindowFocusChanged(focused);
        if (focused) invoke("resumeInput", new Class<?>[0]); else invoke("suspendInput", new Class<?>[0]);
    }
    @Override protected void onDestroy() {
        invoke("suspendInput", new Class<?>[0]);
        super.onDestroy();
        invoke("detach", new Class<?>[0]);
    }
}
