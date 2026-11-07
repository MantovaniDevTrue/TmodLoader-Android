package com.mantovani.tmlmobile;

import android.os.Bundle;
import android.content.pm.ActivityInfo;
import android.os.Build;
import android.view.View;
import android.view.WindowInsets;
import android.view.WindowInsetsController;
import org.libsdl.app.SDLActivity;

public final class SdlGameActivity extends SDLActivity {
    @Override public void setRequestedOrientation(int ignored) {
        super.setRequestedOrientation(ActivityInfo.SCREEN_ORIENTATION_SENSOR_LANDSCAPE);
    }
    private void immersive() {
        setRequestedOrientation(ActivityInfo.SCREEN_ORIENTATION_SENSOR_LANDSCAPE);
        if (Build.VERSION.SDK_INT >= 30) {
            getWindow().setDecorFitsSystemWindows(false);
            WindowInsetsController controller = getWindow().getInsetsController();
            if (controller != null) {
                controller.setSystemBarsBehavior(WindowInsetsController.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);
                controller.hide(WindowInsets.Type.systemBars());
            }
        } else {
            getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY |
                View.SYSTEM_UI_FLAG_FULLSCREEN | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION |
                View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION |
                View.SYSTEM_UI_FLAG_LAYOUT_STABLE);
        }
    }
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
        immersive();
        invoke("attach", new Class<?>[] { android.app.Activity.class }, this);
    }
    @Override protected void onPause() {
        invoke("suspendInput", new Class<?>[0]);
        super.onPause();
    }
    @Override protected void onResume() {
        super.onResume();
        immersive();
        invoke("resumeInput", new Class<?>[0]);
    }
    @Override public void onWindowFocusChanged(boolean focused) {
        super.onWindowFocusChanged(focused);
        if (focused) immersive();
        if (focused) invoke("resumeInput", new Class<?>[0]); else invoke("suspendInput", new Class<?>[0]);
    }
    @Override protected void onDestroy() {
        invoke("suspendInput", new Class<?>[0]);
        super.onDestroy();
        invoke("detach", new Class<?>[0]);
    }
}
