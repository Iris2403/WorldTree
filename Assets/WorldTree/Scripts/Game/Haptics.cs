using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace WorldTree.Game
{
    /// <summary>
    /// Creates the haptics implementation for the current platform: iOS uses UIKit feedback generators
    /// through a small native plugin (Plugins/iOS/WorldTreeHaptics.mm), Android uses the Vibrator service,
    /// everything else (Editor, desktop) does nothing.
    /// </summary>
    public static class Haptics
    {
        public static IHaptics Create()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return new IosHaptics();
#elif UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidHaptics();
#else
            return new NoHaptics();
#endif
        }

        private sealed class NoHaptics : IHaptics
        {
            public void Play(HapticKind kind) { }
        }

#if UNITY_IOS && !UNITY_EDITOR
        private sealed class IosHaptics : IHaptics
        {
            [DllImport("__Internal")]
            private static extern void WT_Haptic(int kind);

            public void Play(HapticKind kind) => WT_Haptic((int)kind);
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private sealed class AndroidHaptics : IHaptics
        {
            private readonly AndroidJavaObject _vibrator;
            private readonly int _sdk;

            public AndroidHaptics()
            {
                try
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                        _sdk = version.GetStatic<int>("SDK_INT");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[WorldTree] Haptics unavailable: " + e.Message);
                }
            }

            public void Play(HapticKind kind)
            {
                if (_vibrator == null) return;
                long ms = kind == HapticKind.Light ? 10 : kind == HapticKind.Medium ? 20 : 30;
                int amplitude = kind == HapticKind.Light ? 60 : kind == HapticKind.Medium ? 140 : 200;
                try
                {
                    if (_sdk >= 26)
                    {
                        using (var effects = new AndroidJavaClass("android.os.VibrationEffect"))
                        using (var effect = effects.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude))
                            _vibrator.Call("vibrate", effect);
                    }
                    else
                    {
                        _vibrator.Call("vibrate", ms);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[WorldTree] Haptic failed: " + e.Message);
                }
            }

            // Never called. Referencing Handheld.Vibrate makes Unity add the VIBRATE permission to the Android manifest.
            private static void RequestVibratePermission() => Handheld.Vibrate();
        }
#endif
    }
}
