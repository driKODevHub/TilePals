using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TinyGiantStudio.DevTools.DevTrails
{
    public class UserStats_Global_UI
    {
        private DevTrailSettings devTrailSettings;

        private UserStats_Global userStats;
        private UserStats_Project userStats_project;

        private Label global_sessionCount;

        private Label unity_usageTime_global;
        private Label activeUsageTime;
        private Label focusedUsageTime;

        private Label compileTime;
        private Label domainReloadTime;
        private Label averageCompileTime;
        private GroupBox global_compilation_groupBox;
        private Label global_compileCounter;
        private GroupBox global_consoleLog_groupBox;
        private Label global_errorLogCounter_editor;
        private Label global_errorLogCounter_playMode;
        private Label global_exceptionLogCounter_editor;
        private Label global_exceptionLogCounter_playMode;
        private Label global_normalLogCounter_editor;
        private Label global_normalLogCounter_playMode;
        private GroupBox global_playMode_groupBox;
        private Label global_playModeCounter;
        private Label global_playModeUsageTime;
        private GroupBox global_sceneOpened_groupBox;
        private Label global_sceneOpenedCounter;
        private GroupBox global_sceneSaved_groupBox;
        private Label global_sceneSavedCounter;
        private GroupBox global_timeStats_groupBox;
        private GroupBox global_undoRedo_groupBox;
        private Label global_undoRedoCounter;
        private Label global_warningLogCounter_editor;
        private Label global_warningLogCounter_playMode;

        private GroupBox devToolsWindowOpenedCounter_groupBox;
        private Label devToolsEditorWindowOpenedCounter;

        private GroupBox editorCrashesGroupBox;


        public UserStats_Global_UI(VisualElement container)
        {
            devTrailSettings = new();

            userStats = UserStats_Global.instance;
            if (userStats.TotalTimeSpentInDomainReload < 0) userStats.TotalTimeSpentInDomainReload = 0;
            userStats_project = UserStats_Project.instance;

            global_timeStats_groupBox = container.Q<GroupBox>("TimeStats");

            global_sessionCount = container.Q<Label>("SessionCount");

            unity_usageTime_global = container.Q<Label>("UsageTime");
            focusedUsageTime = container.Q<Label>("FocusedUsageTime");
            activeUsageTime = container.Q<Label>("ActiveUsageTime");

            global_playMode_groupBox = container.Q<GroupBox>("PlayMode");
            global_playModeCounter = container.Q<Label>("PlayModeCounter");
            global_playModeUsageTime = container.Q<Label>("PlayModeUsageTime");

            global_sceneSaved_groupBox = container.Q<GroupBox>("SceneSaved");
            global_sceneSavedCounter = container.Q<Label>("SceneSavedCounter");
            global_sceneOpened_groupBox = container.Q<GroupBox>("SceneOpened");
            global_sceneOpenedCounter = container.Q<Label>("SceneOpenedCounter");

            global_undoRedo_groupBox = container.Q<GroupBox>("UndoRedo");
            global_undoRedoCounter = container.Q<Label>("UndoRedoCounter");

            global_compilation_groupBox = container.Q<GroupBox>("Compilation");
            global_compileCounter = container.Q<Label>("CompileCounter");
            averageCompileTime = container.Q<Label>("AverageCompileTime");
            compileTime = container.Q<Label>("CompileTime");
            domainReloadTime = container.Q<Label>("DomainReloadTime");

            global_consoleLog_groupBox = container.Q<GroupBox>("ConsoleLogs");

            global_normalLogCounter_playMode = container.Q<Label>("NormalLogCounter_PlayMode");
            global_warningLogCounter_playMode = container.Q<Label>("WarningLogCounter_PlayMode");
            global_exceptionLogCounter_playMode = container.Q<Label>("ExceptionLogCounter_PlayMode");
            global_errorLogCounter_playMode = container.Q<Label>("ErrorLogCounter_PlayMode");

            global_normalLogCounter_editor = container.Q<Label>("NormalLogCounter_Editor");
            global_warningLogCounter_editor = container.Q<Label>("WarningLogCounter_Editor");
            global_exceptionLogCounter_editor = container.Q<Label>("ExceptionLogCounter_Editor");
            global_errorLogCounter_editor = container.Q<Label>("ErrorLogCounter_Editor");

            devToolsWindowOpenedCounter_groupBox = container.Q<GroupBox>("DevToolsEditorWindowOpened");
            devToolsEditorWindowOpenedCounter = devToolsWindowOpenedCounter_groupBox.Q<Label>("DevToolsEditorWindowOpenedCounter");

            editorCrashesGroupBox = container.Q<GroupBox>("EditorCrashes");
            editorCrashesGroupBox.Q<Label>("EditorCrashCounter").text = userStats.ProbableCrashes.ToString();
        }

        public void HideUntrackedData()
        {
            if (devTrailSettings.TrackTime)
                global_timeStats_groupBox.style.display = DisplayStyle.Flex;
            else
                global_timeStats_groupBox.style.display = DisplayStyle.None;

            if (devTrailSettings.TrackSceneSave)
                global_sceneSaved_groupBox.style.display = DisplayStyle.Flex;
            else
                global_sceneSaved_groupBox.style.display = DisplayStyle.None;

            if (devTrailSettings.TrackSceneOpen)
                global_sceneOpened_groupBox.style.display = DisplayStyle.Flex;
            else
                global_sceneOpened_groupBox.style.display = DisplayStyle.None;

            if (devTrailSettings.TrackUndoRedo)
                global_undoRedo_groupBox.style.display = DisplayStyle.Flex;
            else
                global_undoRedo_groupBox.style.display = DisplayStyle.None;

            if (devTrailSettings.TrackPlayMode)
                global_playMode_groupBox.style.display = DisplayStyle.Flex;
            else
                global_playMode_groupBox.style.display = DisplayStyle.None;

            if (devTrailSettings.TrackCompilation)
                global_compilation_groupBox.style.display = DisplayStyle.Flex;
            else
                global_compilation_groupBox.style.display = DisplayStyle.None;

            if (devTrailSettings.TrackConsoleLogs)
                global_consoleLog_groupBox.style.display = DisplayStyle.Flex;
            else
                global_consoleLog_groupBox.style.display = DisplayStyle.None;

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
            global_sessionCount.text = BetterString.Number((int)EditorAnalyticsSessionInfo.sessionCount);

            unity_usageTime_global.text = SmallStringTime(userStats.totalUseTime + CurrentSessionUseTime());
            focusedUsageTime.text = SmallStringTime(userStats.focusedUseTime + CurrentSessionFocusedUseTime());
            activeUsageTime.text = SmallStringTime(userStats.activeUseTime + CurrentSessionActiveUseTime());

            global_playModeCounter.text = BetterString.Number(userStats.EnteredPlayMode);
            global_playModeUsageTime.text = SmallStringTime(userStats.PlayModeUseTime);

            global_sceneSavedCounter.text = BetterString.Number(userStats.SceneSaved);
            global_sceneOpenedCounter.text = BetterString.Number(userStats.SceneOpened);

            global_undoRedoCounter.text = BetterString.Number(userStats.UndoRedoCounter);

            global_compileCounter.text = BetterString.Number(userStats.CompileCounter);

            averageCompileTime.text = userStats.CompileCounter == 0 ? "Not recorded" : SmallStringTime((userStats.TotalTimeSpentCompiling + userStats.TimeSpentCompiling) / userStats.CompileCounter);

            compileTime.text = SmallStringTime(userStats.TotalTimeSpentCompiling + userStats.TimeSpentCompiling);
            domainReloadTime.text = SmallStringTime(Mathf.Abs(userStats.TotalTimeSpentInDomainReload) + userStats.TimeSpentInDomainReload); //Added Mathf.Abs for just in case type situation. Remove later.

            global_normalLogCounter_playMode.text = BetterString.Number(userStats.LogCounterPlayMode);
            global_warningLogCounter_playMode.text = BetterString.Number(userStats.WarningLogCounterPlayMode);
            global_exceptionLogCounter_playMode.text = BetterString.Number(userStats.ExceptionLogCounterPlayMode);
            global_errorLogCounter_playMode.text = BetterString.Number(userStats.ErrorLogCounterPlayMode);

            global_normalLogCounter_editor.text = BetterString.Number(userStats.LogCounterEditor);
            global_warningLogCounter_editor.text = BetterString.Number(userStats.WarningLogCounterEditor);
            global_exceptionLogCounter_editor.text = BetterString.Number(userStats.ExceptionLogCounterEditor);
            global_errorLogCounter_editor.text = BetterString.Number(userStats.ErrorLogCounterEditor);

            devToolsEditorWindowOpenedCounter.text = BetterString.Number(userStats.DevToolsEditorWindowOpened);
        }

        private int CurrentSessionActiveUseTime()
        {
            int activeElapsedTime = (int)TimeSpan.FromMilliseconds(EditorAnalyticsSessionInfo.activeElapsedTime).TotalSeconds;

            //Currently paused
            if (devTrailSettings.PauseTimeTracking)
            {
                var pauseDuration = (int)TimeSpan.FromMilliseconds(EditorAnalyticsSessionInfo.activeElapsedTime).TotalSeconds - EditorPrefs.GetInt("PauseStartActiveTime");
                activeElapsedTime -= pauseDuration;
            }

            if (userStats_project.PauseRecords.sessionID != EditorAnalyticsSessionInfo.id)
                return activeElapsedTime;

            for (int i = 0; i < userStats_project.PauseRecords.sessions.Count; i++)
            {
                activeElapsedTime -= userStats_project.PauseRecords.sessions[i].usageTime;
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

            if (userStats_project.PauseRecords.sessionID != EditorAnalyticsSessionInfo.id)
                return focusedElapsedTime;

            for (int i = 0; i < userStats_project.PauseRecords.sessions.Count; i++)
            {
                focusedElapsedTime -= userStats_project.PauseRecords.sessions[i].focusedTime;
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
            if (userStats_project.PauseRecords.sessionID != EditorAnalyticsSessionInfo.id)
                return timeSinceStartup;

            for (int i = 0; i < userStats_project.PauseRecords.sessions.Count; i++)
            {
                timeSinceStartup -= userStats_project.PauseRecords.sessions[i].usageTime;
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