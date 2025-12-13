using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace TinyGiantStudio.DevTools.DevTrails
{
    public class UserStats_Today_UI
    {
        private UserStats_Today userStats;
        private UserStats_Project userStats_project;
        private UserStats_Global userStats_global;

        private DevTrailSettings devTrailSettings;

        private GroupBox today_timeStats_groupBox;

        private Label usageTimeThisSession;
        private Label focusedUsageTimeThisSession;
        private Label activeUsageTimeThisSession;
        private Label usageTimeToday;
        private Label focusedUsageTimeToday;
        private Label activeUsageTimeToday;

        private GroupBox today_sceneSaved_groupBox;
        private Label today_sceneSavedCounter;
        private GroupBox today_sceneOpened_groupBox;
        private Label today_sceneOpenedCounter;

        private GroupBox today_playMode_groupBox;
        private Label today_playModeCounter;
        private Label today_playModeUsageTime;

        private GroupBox today_undoRedo_groupBox;
        private Label today_undoRedoCounter;

        private GroupBox compilation_groupBox;
        private Label compileCounter;
        private Label averageCompileTime;
        private Label compileTime;
        private Label domainReloadTime;

        private GroupBox consoleLog_groupBox;

        private Label normalLogCounter_playMode;
        private Label warningLogCounter_playMode;
        private Label exceptionLogCounter_playMode;
        private Label errorLogCounter_playMode;

        private Label normalLogCounter_editor;
        private Label warningLogCounter_editor;
        private Label exceptionLogCounter_editor;
        private Label errorLogCounter_editor;

        private GroupBox devToolsWindowOpenedCounter_groupBox;
        private Label devToolsEditorWindowOpenedCounter;

        private GroupBox editorCrashesGroupBox;

        public UserStats_Today_UI(VisualElement container)
        {
            devTrailSettings = new();

            userStats = UserStats_Today.instance;
            if (userStats.TotalTimeSpentInDomainReload < 0) userStats.TotalTimeSpentInDomainReload = 0; 
            userStats_project = UserStats_Project.instance;
            userStats_global = UserStats_Global.instance;

            today_timeStats_groupBox = container.Q<GroupBox>("TimeStats");

            usageTimeThisSession = container.Q<Label>("UsageTimeSession");
            focusedUsageTimeThisSession = container.Q<Label>("FocusedUsageTime");
            activeUsageTimeThisSession = container.Q<Label>("ActiveUsageTime");
            usageTimeToday = container.Q<Label>("UsageTimeSessionToday");
            focusedUsageTimeToday = container.Q<Label>("FocusedUsageTimeToday");
            activeUsageTimeToday = container.Q<Label>("ActiveUsageTimeToday");

            today_sceneSaved_groupBox = container.Q<GroupBox>("SceneSaved");
            today_sceneSavedCounter = container.Q<Label>("SceneSavedCounter");
            today_sceneOpened_groupBox = container.Q<GroupBox>("SceneOpened");
            today_sceneOpenedCounter = container.Q<Label>("SceneOpenedCounter");

            today_playMode_groupBox = container.Q<GroupBox>("PlayMode");
            today_playModeCounter = container.Q<Label>("PlayModeCounter");
            today_playModeUsageTime = container.Q<Label>("PlayModeUsageTime");

            today_undoRedo_groupBox = container.Q<GroupBox>("UndoRedo");
            today_undoRedoCounter = container.Q<Label>("UndoRedoCounter");

            compilation_groupBox = container.Q<GroupBox>("Compilation");
            compileCounter = container.Q<Label>("CompileCounter");
            averageCompileTime = container.Q<Label>("AverageCompileTime");
            compileTime = container.Q<Label>("CompileTime");
            domainReloadTime = container.Q<Label>("DomainReloadTime");

            consoleLog_groupBox = container.Q<GroupBox>("ConsoleLogs");
            normalLogCounter_editor = container.Q<Label>("NormalLogCounter_Editor");
            warningLogCounter_editor = container.Q<Label>("WarningLogCounter_Editor");
            exceptionLogCounter_editor = container.Q<Label>("ExceptionLogCounter_Editor");
            errorLogCounter_editor = container.Q<Label>("ErrorLogCounter_Editor");

            normalLogCounter_playMode = container.Q<Label>("NormalLogCounter_PlayMode");
            warningLogCounter_playMode = container.Q<Label>("WarningLogCounter_PlayMode");
            exceptionLogCounter_playMode = container.Q<Label>("ExceptionLogCounter_PlayMode");
            errorLogCounter_playMode = container.Q<Label>("ErrorLogCounter_PlayMode");

            devToolsWindowOpenedCounter_groupBox = container.Q<GroupBox>("DevToolsEditorWindowOpened");
            devToolsEditorWindowOpenedCounter = devToolsWindowOpenedCounter_groupBox.Q<Label>("DevToolsEditorWindowOpenedCounter");


            editorCrashesGroupBox = container.Q<GroupBox>("EditorCrashes");
            editorCrashesGroupBox.Q<Label>("EditorCrashCounter").text = userStats.ProbableCrashes.ToString();
        }

        public void HideUntrackedData()
        {
            if (devTrailSettings.TrackTime)
                today_timeStats_groupBox.style.display = DisplayStyle.Flex;
            else
                today_timeStats_groupBox.style.display = DisplayStyle.None;

            if (devTrailSettings.TrackSceneSave)
                today_sceneSaved_groupBox.style.display = DisplayStyle.Flex;
            else
                today_sceneSaved_groupBox.style.display = DisplayStyle.None;

            if (devTrailSettings.TrackSceneOpen)
                today_sceneOpened_groupBox.style.display = DisplayStyle.Flex;
            else
                today_sceneOpened_groupBox.style.display = DisplayStyle.None;

            if (devTrailSettings.TrackUndoRedo)
                today_undoRedo_groupBox.style.display = DisplayStyle.Flex;
            else
                today_undoRedo_groupBox.style.display = DisplayStyle.None;

            if (devTrailSettings.TrackPlayMode)
                today_playMode_groupBox.style.display = DisplayStyle.Flex;
            else
                today_playMode_groupBox.style.display = DisplayStyle.None;

            if (devTrailSettings.TrackCompilation)
                compilation_groupBox.style.display = DisplayStyle.Flex;
            else
                compilation_groupBox.style.display = DisplayStyle.None;

            if (devTrailSettings.TrackConsoleLogs)
                consoleLog_groupBox.style.display = DisplayStyle.Flex;
            else
                consoleLog_groupBox.style.display = DisplayStyle.None;

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
            userStats.VerifyNewDay(true);
            usageTimeThisSession.text = SmallStringTime(CurrentSessionUsageTime());
            usageTimeToday.text = SmallStringTime(CurrentSessionUsageTime() + TimeSpentInUnityToday());
            focusedUsageTimeThisSession.text = SmallStringTime(CurrentSessionFocusedUseTime());
            focusedUsageTimeToday.text = SmallStringTime(CurrentSessionFocusedUseTime() + FocusedTimeSpentInUnityToday());
            activeUsageTimeThisSession.text = SmallStringTime(CurrentSessionActiveUseTime());
            activeUsageTimeToday.text = SmallStringTime(CurrentSessionActiveUseTime() + ActiveTimeSpentInUnityToday());

            today_playModeCounter.text = BetterString.Number(userStats.EnteredPlayMode);
            today_playModeUsageTime.text = SmallStringTime(userStats.PlayModeUseTime);

            today_sceneSavedCounter.text = BetterString.Number(userStats.SceneSaved);
            today_sceneOpenedCounter.text = BetterString.Number(userStats.SceneOpened);

            today_undoRedoCounter.text = BetterString.Number(userStats.UndoRedoCounter);

            compileCounter.text = BetterString.Number(userStats.CompileCounter);

            averageCompileTime.text = userStats.CompileCounter == 0 ? "Not recorded" : SmallStringTime((userStats.TotalTimeSpentCompiling + userStats.TimeSpentCompiling) / userStats.CompileCounter);

            compileTime.text = SmallStringTime(userStats.TotalTimeSpentCompiling + userStats.TimeSpentCompiling);
            domainReloadTime.text = SmallStringTime(MathF.Abs(userStats.TotalTimeSpentInDomainReload) + userStats.TimeSpentInDomainReload);

            normalLogCounter_editor.text = BetterString.Number(userStats.LogCounter_editor);
            warningLogCounter_editor.text = BetterString.Number(userStats.WarningLogCounter_editor);
            exceptionLogCounter_editor.text = BetterString.Number(userStats.ExceptionLogCounter_editor);
            errorLogCounter_editor.text = BetterString.Number(userStats.ErrorLogCounter_editor);

            normalLogCounter_playMode.text = BetterString.Number(userStats.LogCounter_playMode);
            warningLogCounter_playMode.text = BetterString.Number(userStats.WarningLogCounter_playMode);
            exceptionLogCounter_playMode.text = BetterString.Number(userStats.ExceptionLogCounter_playMode);
            errorLogCounter_playMode.text = BetterString.Number(userStats.ErrorLogCounter_playMode);

            devToolsEditorWindowOpenedCounter.text = BetterString.Number(userStats.DevToolsEditorWindowOpened);
        }

        public double CurrentSessionUsageTime()
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

        private int TimeSpentInUnityToday()
        {
            if (userStats_global.DayRecords != null)
            {
                if (userStats_global.DayRecords.Count > 0)
                {
                    UserStats_Global.DayRecord lastRecord = userStats_global.DayRecords[userStats_global.DayRecords.Count - 1];

                    if (lastRecord.date.SameDay(DateTime.Now))
                    {
                        int timeSpent = 0;
                        for (int i = 0; i < lastRecord.Sessions.Count; i++)
                        {
                            timeSpent += lastRecord.Sessions[i].totalTime;
                        }
                        return timeSpent;
                    }
                }
            }

            return 0;
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

        private int FocusedTimeSpentInUnityToday()
        {
            if (userStats_global.DayRecords != null)
            {
                if (userStats_global.DayRecords.Count > 0)
                {
                    UserStats_Global.DayRecord lastRecord = userStats_global.DayRecords[userStats_global.DayRecords.Count - 1];

                    if (lastRecord.date.SameDay(DateTime.Now))
                    {
                        int timeSpent = 0;
                        for (int i = 0; i < lastRecord.Sessions.Count; i++)
                        {
                            timeSpent += lastRecord.Sessions[i].focusedUseTime;
                        }
                        return timeSpent;
                    }
                }
            }

            return 0;
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

            if (userStats_project.PauseRecords.sessionID != EditorAnalyticsSessionInfo.id)
                return activeElapsedTime;

            for (int i = 0; i < userStats_project.PauseRecords.sessions.Count; i++)
            {
                //activeElapsedTime -= userStats_project.PauseRecords.sessions[i].usageTime;
                activeElapsedTime -= userStats_project.PauseRecords.sessions[i].activeTime;
            }
            activeElapsedTime = Math.Abs(activeElapsedTime);

            return activeElapsedTime;
        }

        private int ActiveTimeSpentInUnityToday()
        {
            if (userStats_global.DayRecords != null)
            {
                if (userStats_global.DayRecords.Count > 0)
                {
                    UserStats_Global.DayRecord lastRecord = userStats_global.DayRecords[userStats_global.DayRecords.Count - 1];
                    if (lastRecord.date.SameDay(DateTime.Now))
                    {
                        int timeSpent = 0;
                        for (int i = 0; i < lastRecord.Sessions.Count; i++)
                        {
                            timeSpent += lastRecord.Sessions[i].activeUseTime;
                        }
                        return timeSpent;
                    }
                }
            }

            return 0;
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