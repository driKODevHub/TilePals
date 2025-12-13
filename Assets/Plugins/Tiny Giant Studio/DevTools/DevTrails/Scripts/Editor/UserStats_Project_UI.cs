using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace TinyGiantStudio.DevTools.DevTrails
{
    public class UserStats_Project_UI
    {
        private UserStats_Project userStats;
        private DevTrailSettings devTrailSettings;

        private GroupBox project_timeStats_groupBox;

        private Label project_usageTime;
        private Label project_focusedUsageTime;
        private Label project_activeUsageTime;

        private GroupBox project_sceneSaved_groupBox;
        private Label project_sceneSavedCounter;
        private GroupBox project_sceneOpened_groupBox;
        private Label project_sceneOpenedCounter;

        private GroupBox project_undoRedo_groupBox;
        private Label project_undoRedoCounter;

        private GroupBox project_playMode_groupBox;
        private Label project_playModeCounter;
        private Label project_playModeUsageTime;

        private GroupBox project_compilation_groupBox;
        private Label project_compileCounter;
        private Label averageCompileTime;
        private Label compileTime;
        private Label domainReloadTime;

        private GroupBox project_consoleLog_groupBox;

        private Label project_normalLogCounter_playMode;
        private Label project_warningLogCounter_playMode;
        private Label project_exceptionLogCounter_playMode;
        private Label project_errorLogCounter_playMode;

        private Label project_normalLogCounter_editor;
        private Label project_warningLogCounter_editor;
        private Label project_exceptionLogCounter_editor;
        private Label project_errorLogCounter_editor;

        private GroupBox devToolsWindowOpenedCounter_groupBox;
        private Label devToolsEditorWindowOpenedCounter;

        private GroupBox editorCrashesGroupBox;

        public UserStats_Project_UI(VisualElement container)
        {
            devTrailSettings = new();
            
            userStats = UserStats_Project.instance;
            if (userStats.TotalTimeSpentInDomainReload < 0) userStats.TotalTimeSpentInDomainReload = 0;

            project_timeStats_groupBox = container.Q<GroupBox>("TimeStats");

            project_usageTime = container.Q<Label>("UsageTime");
            project_focusedUsageTime = container.Q<Label>("FocusedUsageTime");
            project_activeUsageTime = container.Q<Label>("ActiveUsageTime");

            project_sceneSaved_groupBox = container.Q<GroupBox>("SceneSaved");
            project_sceneSavedCounter = container.Q<Label>("SceneSavedCounter");
            project_sceneOpened_groupBox = container.Q<GroupBox>("SceneOpened");
            project_sceneOpenedCounter = container.Q<Label>("SceneOpenedCounter");

            project_playMode_groupBox = container.Q<GroupBox>("PlayMode");
            project_playModeCounter = container.Q<Label>("PlayModeCounter");
            project_playModeUsageTime = container.Q<Label>("PlayModeUsageTime");

            project_undoRedo_groupBox = container.Q<GroupBox>("UndoRedo");
            project_undoRedoCounter = container.Q<Label>("UndoRedoCounter");

            project_compilation_groupBox = container.Q<GroupBox>("Compilation");
            project_compileCounter = container.Q<Label>("CompileCounter");
            averageCompileTime = container.Q<Label>("AverageCompileTime");
            compileTime = container.Q<Label>("CompileTime");
            domainReloadTime = container.Q<Label>("DomainReloadTime");

            project_consoleLog_groupBox = container.Q<GroupBox>("ConsoleLogs");
            project_normalLogCounter_playMode = container.Q<Label>("NormalLogCounter_PlayMode");
            project_warningLogCounter_playMode = container.Q<Label>("WarningLogCounter_PlayMode");
            project_exceptionLogCounter_playMode = container.Q<Label>("ExceptionLogCounter_PlayMode");
            project_errorLogCounter_playMode = container.Q<Label>("ErrorLogCounter_PlayMode");

            project_normalLogCounter_editor = container.Q<Label>("NormalLogCounter_Editor");
            project_warningLogCounter_editor = container.Q<Label>("WarningLogCounter_Editor");
            project_exceptionLogCounter_editor = container.Q<Label>("ExceptionLogCounter_Editor");
            project_errorLogCounter_editor = container.Q<Label>("ErrorLogCounter_Editor");

            devToolsWindowOpenedCounter_groupBox = container.Q<GroupBox>("DevToolsEditorWindowOpened");
            devToolsEditorWindowOpenedCounter = devToolsWindowOpenedCounter_groupBox.Q<Label>("DevToolsEditorWindowOpenedCounter");

            editorCrashesGroupBox = container.Q<GroupBox>("EditorCrashes");
            editorCrashesGroupBox.Q<Label>("EditorCrashCounter").text = userStats.probablyCrashes.ToString();
        }

        public void HideUntrackedData()
        {
            if (devTrailSettings.TrackTime)
                project_timeStats_groupBox.style.display = DisplayStyle.Flex;
            else
                project_timeStats_groupBox.style.display = DisplayStyle.None;

            if (devTrailSettings.TrackSceneSave)
                project_sceneSaved_groupBox.style.display = DisplayStyle.Flex;
            else
                project_sceneSaved_groupBox.style.display = DisplayStyle.None;

            if (devTrailSettings.TrackSceneOpen)
                project_sceneOpened_groupBox.style.display = DisplayStyle.Flex;
            else
                project_sceneOpened_groupBox.style.display = DisplayStyle.None;

            if (devTrailSettings.TrackUndoRedo)
                project_undoRedo_groupBox.style.display = DisplayStyle.Flex;
            else
                project_undoRedo_groupBox.style.display = DisplayStyle.None;

            if (devTrailSettings.TrackPlayMode)
                project_playMode_groupBox.style.display = DisplayStyle.Flex;
            else
                project_playMode_groupBox.style.display = DisplayStyle.None;

            if (devTrailSettings.TrackCompilation)
                project_compilation_groupBox.style.display = DisplayStyle.Flex;
            else
                project_compilation_groupBox.style.display = DisplayStyle.None;

            if (devTrailSettings.TrackConsoleLogs)
                project_consoleLog_groupBox.style.display = DisplayStyle.Flex;
            else
                project_consoleLog_groupBox.style.display = DisplayStyle.None;

            if (devTrailSettings.ShowDevToolsEditorWindowTrack)
                devToolsWindowOpenedCounter_groupBox.style.display = DisplayStyle.Flex;
            else
                devToolsWindowOpenedCounter_groupBox.style.display = DisplayStyle.None;

            if (devTrailSettings.TrackEditorCrashes)
                editorCrashesGroupBox.style.display = DisplayStyle.Flex;
            else
                editorCrashesGroupBox.style.display = DisplayStyle.None;
        }

        internal void UpdateInfo()
        {
            project_usageTime.text = SmallStringTime(userStats.totalUseTime + CurrentSessionUseTime());
            project_focusedUsageTime.text = SmallStringTime(userStats.focusedUseTime + CurrentSessionFocusedUseTime());
            project_activeUsageTime.text = SmallStringTime(userStats.activeUseTime + CurrentSessionActiveUseTime());

            project_playModeCounter.text = BetterString.Number(userStats.EnteredPlayMode);
            project_playModeUsageTime.text = SmallStringTime(userStats.PlayModeUseTime);

            project_sceneSavedCounter.text = BetterString.Number(userStats.SceneSaved);
            project_sceneOpenedCounter.text = BetterString.Number(userStats.SceneOpened);

            project_undoRedoCounter.text = BetterString.Number(userStats.UndoRedoCounter);

            project_compileCounter.text = BetterString.Number(userStats.CompileCounter);

            averageCompileTime.text = userStats.CompileCounter == 0 ? "Not recorded" : SmallStringTime((userStats.TotalTimeSpentCompiling + userStats.TimeSpentCompiling) / userStats.CompileCounter);

            compileTime.text = SmallStringTime(userStats.TotalTimeSpentCompiling + userStats.TimeSpentCompiling);
            domainReloadTime.text = SmallStringTime(MathF.Abs(userStats.TotalTimeSpentInDomainReload) + userStats.TimeSpentInDomainReload);

            project_normalLogCounter_playMode.text = BetterString.Number(userStats.LogCounter_playMode);
            project_warningLogCounter_playMode.text = BetterString.Number(userStats.WarningLogCounter_playMode);
            project_exceptionLogCounter_playMode.text = BetterString.Number(userStats.ExceptionLogCounter_playMode);
            project_errorLogCounter_playMode.text = BetterString.Number(userStats.ErrorLogCounter_playMode);

            project_normalLogCounter_editor.text = BetterString.Number(userStats.LogCounter_editor);
            project_warningLogCounter_editor.text = BetterString.Number(userStats.WarningLogCounter_editor);
            project_exceptionLogCounter_editor.text = BetterString.Number(userStats.ExceptionLogCounter_editor);
            project_errorLogCounter_editor.text = BetterString.Number(userStats.ErrorLogCounter_editor);

            devToolsEditorWindowOpenedCounter.text = BetterString.Number(userStats.DevToolsEditorWindowOpened);
        }

        private int CurrentSessionActiveUseTime()
        {
            var activeElapsedTime = (int)TimeSpan.FromMilliseconds(EditorAnalyticsSessionInfo.activeElapsedTime).TotalSeconds;

            //Currently paused
            if (devTrailSettings.PauseTimeTracking)
            {
                var pauseDuration = (int)TimeSpan.FromMilliseconds(EditorAnalyticsSessionInfo.activeElapsedTime).TotalSeconds - EditorPrefs.GetInt("PauseStartActiveTime");
                activeElapsedTime -= pauseDuration;
            }

            if (userStats.PauseRecords.sessionID != EditorAnalyticsSessionInfo.id)
                return activeElapsedTime;

            for (int i = 0; i < userStats.PauseRecords.sessions.Count; i++)
            {
                activeElapsedTime -= userStats.PauseRecords.sessions[i].usageTime;
            }
            activeElapsedTime = Math.Abs(activeElapsedTime);

            return activeElapsedTime;
        }

        private int CurrentSessionFocusedUseTime()
        {
            int focusedElapsedTime = (int)TimeSpan.FromMilliseconds(EditorAnalyticsSessionInfo.focusedElapsedTime).TotalSeconds;

            //Currently paused
            if (devTrailSettings.PauseTimeTracking)
            {
                var pauseDuration = (int)TimeSpan.FromMilliseconds(EditorAnalyticsSessionInfo.focusedElapsedTime).TotalSeconds - EditorPrefs.GetInt("PauseStartFocusedTime");
                focusedElapsedTime -= pauseDuration;
                //(int)TimeSpan.FromMilliseconds(EditorAnalyticsSessionInfo.activeElapsedTime).TotalSeconds - EditorPrefs.GetInt("PauseStartActiveTime");
            }

            if (userStats.PauseRecords.sessionID != EditorAnalyticsSessionInfo.id)
                return focusedElapsedTime;

            for (int i = 0; i < userStats.PauseRecords.sessions.Count; i++)
            {
                focusedElapsedTime -= userStats.PauseRecords.sessions[i].focusedTime;
            }
            focusedElapsedTime = Math.Abs(focusedElapsedTime);

            return focusedElapsedTime;
        }

        private double CurrentSessionUseTime()
        {
            var timeSinceStartup = EditorApplication.timeSinceStartup;

            //Currently paused
            if (devTrailSettings.PauseTimeTracking)
            {
                var pauseDuration = timeSinceStartup - EditorPrefs.GetInt("PauseStartUsageTime");
                timeSinceStartup -= pauseDuration;
            }

            //Check old pause sessions
            if (userStats.PauseRecords.sessionID != EditorAnalyticsSessionInfo.id)
                return timeSinceStartup;

            for (int i = 0; i < userStats.PauseRecords.sessions.Count; i++)
            {
                timeSinceStartup -= userStats.PauseRecords.sessions[i].usageTime;
            }
            timeSinceStartup = Math.Abs(timeSinceStartup);

            return timeSinceStartup;
        }

        private string SmallStringTime(double time)
        {
            TimeSpan t = TimeSpan.FromSeconds(time);

            if (t.Days > 0)
                return string.Format("{0:D1}d {1:D1}h {2:D2}m", t.Days, t.Hours, t.Minutes);
            else if (t.Hours > 0)
            {
                return string.Format("{0:D1}h {1:D2}m", t.Hours, t.Minutes);
            }
            else
            {
                if (t.Minutes > 0) //hour haven't reached
                {
                    return string.Format("{0:D2}m {1:D2}s", t.Minutes, t.Seconds);
                }
                else //minute haven't reached
                {
                    if (t.Seconds > 0)
                        return string.Format("{0:D2}s", t.Seconds);
                    else
                        return string.Format("{0:D2}ms", t.Milliseconds);
                }
            }
            //return string.Format("{0:D1}h {1:D2}m {2:D1}s", t.Hours, t.Minutes, t.Seconds);
        }
    }
}