// <copyright file="SettingsDialog.cs" company="Google Inc.">
// Copyright (C) 2015 Google Inc. All Rights Reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//    limitations under the License.
// </copyright>

namespace GooglePlayServices
{
    using System;
    using System.IO;
    using UnityEditor;
    using UnityEngine;
    using Google;

    /// <summary>
    /// Settings dialog for PlayServices Resolver.
    /// </summary>
    public class SettingsDialog : EditorWindow
    {
        /// <summary>
        /// Loads / saves settings for this dialog.
        /// </summary>
        private class Settings {
            internal bool enableAutoResolution;
            internal bool prebuildWithGradle;
            internal bool useGradleDaemon;
            internal bool installAndroidPackages;
            internal string packageDir;
            internal bool explodeAars;
            internal bool verboseLogging;
            internal bool autoResolutionDisabledWarning;
            internal bool useProjectSettings;

            /// <summary>
            /// Load settings into the dialog.
            /// </summary>
            internal Settings() {
                enableAutoResolution = SettingsDialog.EnableAutoResolution;
                prebuildWithGradle = SettingsDialog.PrebuildWithGradle;
                useGradleDaemon = SettingsDialog.UseGradleDaemon;
                installAndroidPackages = SettingsDialog.InstallAndroidPackages;
                packageDir = SettingsDialog.PackageDir;
                explodeAars = SettingsDialog.ExplodeAars;
                verboseLogging = SettingsDialog.VerboseLogging;
                autoResolutionDisabledWarning = SettingsDialog.AutoResolutionDisabledWarning;
                useProjectSettings = SettingsDialog.UseProjectSettings;
            }

            /// <summary>
            /// Save dialog settings to preferences.
            /// </summary>
            internal void Save() {
                SettingsDialog.PrebuildWithGradle = prebuildWithGradle;
                SettingsDialog.UseGradleDaemon = useGradleDaemon;
                SettingsDialog.EnableAutoResolution = enableAutoResolution;
                SettingsDialog.InstallAndroidPackages = installAndroidPackages;
                if (SettingsDialog.ConfigurablePackageDir) SettingsDialog.PackageDir = packageDir;
                SettingsDialog.ExplodeAars = explodeAars;
                SettingsDialog.VerboseLogging = verboseLogging;
                SettingsDialog.AutoResolutionDisabledWarning = autoResolutionDisabledWarning;
                SettingsDialog.UseProjectSettings = useProjectSettings;
            }
        }

        const string Namespace = "GooglePlayServices.";
        private const string AutoResolveKey = Namespace + "AutoResolverEnabled";
        private const string PrebuildWithGradleKey = Namespace + "PrebuildWithGradle";
        private const string PackageInstallKey = Namespace + "AndroidPackageInstallationEnabled";
        private const string PackageDirKey = Namespace + "PackageDirectory";
        private const string ExplodeAarsKey = Namespace + "ExplodeAars";
        private const string VerboseLoggingKey = Namespace + "VerboseLogging";
        private const string AutoResolutionDisabledWarningKey =
            Namespace + "AutoResolutionDisabledWarning";
        private const string UseGradleDaemonKey = Namespace + "UseGradleDaemon";
        // List of preference keys, used to restore default settings.
        private static string[] PreferenceKeys = new [] {
            AutoResolveKey,
            PrebuildWithGradleKey,
            PackageInstallKey,
            PackageDirKey,
            ExplodeAarsKey,
            VerboseLoggingKey,
            AutoResolutionDisabledWarningKey,
            UseGradleDaemonKey
        };

        private const string AndroidPluginsDir = "Assets/Plugins/Android";

        // Unfortunately, Unity currently does not search recursively search subdirectories of
        // AndroidPluginsDir for Android library plugins.  When this is supported - or we come up
        // with a workaround - this can be enabled.
        static bool ConfigurablePackageDir = false;
        static string DefaultPackageDir = AndroidPluginsDir;

        private Settings settings;

        private static ProjectSettings projectSettings = new ProjectSettings(Namespace);

        // Previously validated package directory.
        private static string previouslyValidatedPackageDir;

        /// <summary>
        /// Reset settings of this plugin to default values.
        /// </summary>
        internal static void RestoreDefaultSettings() {
            projectSettings.DeleteKeys(PreferenceKeys);
        }

        internal static bool EnableAutoResolution {
            set { projectSettings.SetBool(AutoResolveKey, value); }
            get { return projectSettings.GetBool(AutoResolveKey, true); }
        }

        internal static bool PrebuildWithGradle {
            private set { projectSettings.SetBool(PrebuildWithGradleKey, value); }
            get { return projectSettings.GetBool(PrebuildWithGradleKey, false); }
        }

        internal static bool UseGradleDaemon {
            private set { projectSettings.SetBool(UseGradleDaemonKey, value); }
            get { return projectSettings.GetBool(UseGradleDaemonKey, false); }
        }

        internal static bool InstallAndroidPackages {
            private set { projectSettings.SetBool(PackageInstallKey, value); }
            get { return projectSettings.GetBool(PackageInstallKey, true); }
        }


        internal static string PackageDir {
            private set { projectSettings.SetString(PackageDirKey, value); }
            get {
                return ValidatePackageDir(
                    ConfigurablePackageDir ?
                        (projectSettings.GetString(PackageDirKey, DefaultPackageDir)) :
                        DefaultPackageDir);
            }
        }

        internal static bool AutoResolutionDisabledWarning {
            set { projectSettings.SetBool(AutoResolutionDisabledWarningKey, value); }
            get { return projectSettings.GetBool(AutoResolutionDisabledWarningKey, true); }
        }

        internal static bool UseProjectSettings {
            get { return projectSettings.UseProjectSettings; }
            set { projectSettings.UseProjectSettings = value; }
        }

        // Whether AARs that use variable expansion should be exploded when Gradle builds are
        // enabled.
        internal static bool ExplodeAars {
            private set { projectSettings.SetBool(ExplodeAarsKey, value); }
            get { return projectSettings.GetBool(ExplodeAarsKey, true); }
        }

        internal static bool VerboseLogging {
            private set { projectSettings.SetBool(VerboseLoggingKey, value); }
            get { return projectSettings.GetBool(VerboseLoggingKey, false); }
        }

        internal static string ValidatePackageDir(string directory) {
            // Make sure the package directory starts with the same name.
            // This is case insentitive to handle cases where developers rename Unity
            // project directories on Windows (which has a case insensitive file system by
            // default) then they use the project on OSX / Linux.
            if (!directory.ToLowerInvariant().StartsWith(AndroidPluginsDir.ToLower())) {
                directory = AndroidPluginsDir;
            }
            var searchDirectory = FileUtils.FindDirectoryByCaseInsensitivePath(directory);
            if (directory != searchDirectory &&
                (previouslyValidatedPackageDir == null ||
                 searchDirectory != previouslyValidatedPackageDir)) {
                PlayServicesResolver.Log(
                    String.Format("Resolving to Android package directory {0} instead of the " +
                                  "requested target directory {1}\n" +
                                  "\n" +
                                  "Is {0} in a different case to {1} ?\n",
                                  searchDirectory, directory), level: LogLevel.Warning);
                directory = searchDirectory;
            } else if (previouslyValidatedPackageDir == null ||
                       searchDirectory != previouslyValidatedPackageDir){
                PlayServicesResolver.Log(
                    String.Format("Android package directory {0} not found.",
                                  directory), level: LogLevel.Warning);
            }
            previouslyValidatedPackageDir = searchDirectory;

            return directory;
        }

        public void Initialize() {
            minSize = new Vector2(350, 300);
            position = new Rect(UnityEngine.Screen.width / 3, UnityEngine.Screen.height / 3,
                                minSize.x, minSize.y);
        }

        public void LoadSettings() {
            settings = new Settings();
        }

        public void OnEnable() {
            LoadSettings();
        }

        /// <summary>
        /// Called when the GUI should be rendered.
        /// </summary>
        public void OnGUI() {
            GUI.skin.label.wordWrap = true;
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Prebuild With Gradle (Experimental)", EditorStyles.boldLabel);
            settings.prebuildWithGradle = EditorGUILayout.Toggle(settings.prebuildWithGradle);
            GUILayout.EndHorizontal();

            EditorGUI.BeginDisabledGroup(settings.prebuildWithGradle == true);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Use Gradle Daemon", EditorStyles.boldLabel);
            settings.useGradleDaemon = EditorGUILayout.Toggle(settings.useGradleDaemon);
            GUILayout.EndHorizontal();
            GUILayout.Label(
                settings.useGradleDaemon ?
                ("Gradle Daemon will be used to fetch dependencies.  " +
                 "This is faster but can be flakey in some environments.") :
                ("Gradle Daemon will not be used.  This is slow but reliable."));
            EditorGUI.EndDisabledGroup();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Enable Auto-Resolution", EditorStyles.boldLabel);
            settings.enableAutoResolution = EditorGUILayout.Toggle(settings.enableAutoResolution);
            GUILayout.EndHorizontal();

            EditorGUI.BeginDisabledGroup(settings.prebuildWithGradle == true);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Install Android Packages", EditorStyles.boldLabel);
            settings.installAndroidPackages =
                EditorGUILayout.Toggle(settings.installAndroidPackages);
            GUILayout.EndHorizontal();

            if (ConfigurablePackageDir) {
                GUILayout.BeginHorizontal();
                string previousPackageDir = settings.packageDir;
                GUILayout.Label("Package Directory", EditorStyles.boldLabel);
                if (GUILayout.Button("Browse")) {
                    string path = EditorUtility.OpenFolderPanel("Set Package Directory",
                                                                PackageDir, "");
                    int startOfPath = path.IndexOf(AndroidPluginsDir);
                    settings.packageDir = startOfPath < 0 ? "" :
                        path.Substring(startOfPath, path.Length - startOfPath);;
                }
                if (!previousPackageDir.Equals(settings.packageDir)) {
                    settings.packageDir = ValidatePackageDir(settings.packageDir);
                }
                GUILayout.EndHorizontal();
                settings.packageDir = EditorGUILayout.TextField(settings.packageDir);
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Explode AARs", EditorStyles.boldLabel);
            settings.explodeAars = EditorGUILayout.Toggle(settings.explodeAars);
            GUILayout.EndHorizontal();
            if (settings.explodeAars) {
                GUILayout.Label("AARs will be exploded (unpacked) when ${applicationId} " +
                                "variable replacement is required in an AAR's " +
                                "AndroidManifest.xml or a single target ABI is selected " +
                                "without a compatible build system.");
            } else {
                GUILayout.Label("AAR explosion will be disabled in exported Gradle builds " +
                                "(Unity 5.5 and above). You will need to set " +
                                "android.defaultConfig.applicationId to your bundle ID in your " +
                                "build.gradle to generate a functional APK.");
            }
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginDisabledGroup(settings.enableAutoResolution);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Auto-Resolution Disabled Warning", EditorStyles.boldLabel);
            settings.autoResolutionDisabledWarning =
                EditorGUILayout.Toggle(settings.autoResolutionDisabledWarning);
            GUILayout.EndHorizontal();
            EditorGUI.EndDisabledGroup();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Verbose Logging", EditorStyles.boldLabel);
            settings.verboseLogging = EditorGUILayout.Toggle(settings.verboseLogging);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Use project settings", EditorStyles.boldLabel);
            settings.useProjectSettings = EditorGUILayout.Toggle(settings.useProjectSettings);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (GUILayout.Button("Reset to Defaults")) {
                // Load default settings into the dialog but preserve the state in the user's
                // saved preferences.
                var backupSettings = new Settings();
                RestoreDefaultSettings();
                LoadSettings();
                backupSettings.Save();
            }

            GUILayout.BeginHorizontal();
            bool closeWindow = GUILayout.Button("Cancel");
            bool ok = GUILayout.Button("OK");
            closeWindow |= ok;
            if (ok) {
                settings.Save();
                PlayServicesResolver.OnSettingsChanged();
            }
            if (closeWindow) Close();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }
    }
}

