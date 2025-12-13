using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TinyGiantStudio.DevTools.DevTrails
{
    public class Page_UserStats : TabPage
    {
        // Inherit base class constructor
        public Page_UserStats(DevToolsWindow projectManagerWindow, VisualElement container) : base(projectManagerWindow, container)
        {
            tabName = "DevTrails";
            tabShortName = "Trails";
            tabIcon = "devTrails-icon";
            priority = 3; //Determines the order in tabs list
        }

        private UserStats_Today userStats_Today;
        private UserStats_Today_UI userStats_Today_UI;
        private UserStats_Project_UI userStats_Project_UI;
        private UserStats_Global userStats_global;
        private UserStats_Global_UI userStats_Global_UI;

        private Label usageGoalLabel;

        //The schedule to refresh data is tied to this.
        //So, it's easier control schedule like stopping it and restarting
        private VisualElement scheduleContainer;

        private DevTrailSettings settings;

        public override void SetupPage(DevToolsWindow newDevToolsWindow, VisualElement container)
        {
            base.devToolsWindow = newDevToolsWindow;

            VisualTreeAsset pageAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Plugins/Tiny Giant Studio/DevTools/DevTrails/Scripts/Editor/Pages/Stats/PageUserStats.uxml");
            //If the asset isn't found in the correct location,
            if (pageAsset == null)
                //Check the testing location
                pageAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/DevTrails/Scripts/Editor/Pages/Stats/PageUserStats.uxml");

            pageContainer = new();
            pageContainer.AddToClassList("flatContainer");
            pageAsset.CloneTree(pageContainer);
            container.Q<GroupBox>("Content").Add(pageContainer);

            userStats_Project_UI = new UserStats_Project_UI(pageContainer.Q<GroupBox>("ProjectStatsSummary"));
            userStats_Today_UI = new UserStats_Today_UI(pageContainer.Q<GroupBox>("TodaysUnityStatsSummary"));
            userStats_Global_UI = new UserStats_Global_UI(pageContainer.Q<GroupBox>("GlobalStats"));

            userStats_Today = UserStats_Today.instance;
            UserStats_Today.instance.DevToolsEditorWindowOpened++;
            UserStats_Project.instance.DevToolsEditorWindowOpened++;
            userStats_global = UserStats_Global.instance;
            UserStats_Global.instance.DevToolsEditorWindowOpened++;

            settings = new DevTrailSettings();

            var pauseTimeButton = pageContainer.Q<Button>("PauseTrackingTime");
            var resumeTimeButton = pageContainer.Q<Button>("ResumeTrackingTime");

            //If time tracking is stopped without keeping track of the session, editor crashed and start tracking again.
            if (settings.PauseTimeTracking && !EditorPrefs.HasKey("PauseStartUsageTime"))
                settings.PauseTimeTracking = false;

            pauseTimeButton.clicked += () =>
        {
            settings.PauseTimeTracking = true;
            UpdateTimePauseResumeButton(pauseTimeButton, resumeTimeButton);
        };
            resumeTimeButton.clicked += () =>
            {
                settings.PauseTimeTracking = false;
                UpdateTimePauseResumeButton(pauseTimeButton, resumeTimeButton);
            };
            UpdateTimePauseResumeButton(pauseTimeButton, resumeTimeButton);

            var startAlarmButton = pageContainer.Q<Button>("StartAlarmButton");
            var stopAlarmButton = pageContainer.Q<Button>("StopAlarmButton");
            usageGoalLabel = stopAlarmButton.Q<Label>("UsageGoalLabel");
            startAlarmButton.clicked += () =>
            {
                settings.EnabledUsageGoal = true;
                UpdateUsageGoalButton(startAlarmButton, stopAlarmButton);
            };
            stopAlarmButton.clicked += () =>
            {
                settings.EnabledUsageGoal = false;
                UpdateUsageGoalButton(startAlarmButton, stopAlarmButton);
            };
            UpdateUsageGoalButton(startAlarmButton, stopAlarmButton);

            UpdateUsageGoal();
        }

        private void UpdateUsageGoalButton(Button startAlarmButton, Button stopAlarmButton)
        {
            if (!settings.TrackTime)
            {
                startAlarmButton.style.display = DisplayStyle.None;
                stopAlarmButton.style.display = DisplayStyle.None;

                return;
            }

            if (settings.EnabledUsageGoal)
            {
                startAlarmButton.style.display = DisplayStyle.None;
                stopAlarmButton.style.display = DisplayStyle.Flex;
            }
            else
            {
                startAlarmButton.style.display = DisplayStyle.Flex;
                stopAlarmButton.style.display = DisplayStyle.None;
            }
        }

        private void UpdateTimePauseResumeButton(Button pauseTimeButton, Button resumeTimeButton)
        {
            if (!settings.TrackTime)
            {
                pauseTimeButton.style.display = DisplayStyle.None;
                resumeTimeButton.style.display = DisplayStyle.None;

                return;
            }

            if (settings.PauseTimeTracking)
            {
                pauseTimeButton.style.display = DisplayStyle.None;
                resumeTimeButton.style.display = DisplayStyle.Flex;
            }
            else
            {
                pauseTimeButton.style.display = DisplayStyle.Flex;
                resumeTimeButton.style.display = DisplayStyle.None;
            }
        }

        public override void OpenPage()
        {
            pageContainer.style.display = DisplayStyle.Flex;

            HideUntrackedData();
        }

        public override void ClosePage()
        {
            pageContainer.style.display = DisplayStyle.None;

            if (scheduleContainer != null)
            {
                pageContainer.Remove(scheduleContainer);
                scheduleContainer = null;
            }
        }

        public override void UpdatePage()
        {
            if (scheduleContainer != null)
            {
                pageContainer.Remove(scheduleContainer);
                scheduleContainer = null;
            }

            scheduleContainer = new VisualElement();
            pageContainer.Add(scheduleContainer);

            scheduleContainer.schedule.Execute(() =>
            {
                UpdateInfo();
                //}).Every(120000).ExecuteLater(60000);
            }).Every(1000).ExecuteLater(1000); //todo control update rate by settings file

            UpdateInfo();
        }

        private void HideUntrackedData()
        {
            userStats_Project_UI.HideUntrackedData();
            userStats_Global_UI.HideUntrackedData();
            userStats_Today_UI.HideUntrackedData();
        }

        private void UpdateInfo()
        {
            userStats_Project_UI.UpdateInfo();
            userStats_Global_UI.UpdateInfo();
            userStats_Today_UI.UpdateInfo();

            UpdateUsageGoal();
        }

        private void UpdateUsageGoal()
        {
            if (settings.TrackTime)
            {
                if (settings.EnabledUsageGoal)
                {
                    int usageGoal = settings.UsageGoal;
                    int elapsedTime = TimeSpentInUnityToday() + (int)userStats_Today_UI.CurrentSessionUsageTime();

                    if (usageGoal > elapsedTime)
                    {
                        int timeLeft = usageGoal - elapsedTime;
                        usageGoalLabel.text = "Only " + SmallStringTime(timeLeft) + " until you have reached your goal.";
                    }
                    else
                    {
                        if (settings.UsageGoalPopUp && !Application.isPlaying)
                        {
                            if (!userStats_Today.showedUsagePopUpToday)
                            {
                                userStats_Today.showedUsagePopUpToday = true;
                                userStats_Today.SaveToDisk();

                                EditorUtility.DisplayDialog("Usage goal reached!", "You have used unity for " + SmallStringTime(elapsedTime) + " today and reached your usage goal of " + SmallStringTime(usageGoal) + ".", "Ok");
                                //DisplayDialog()
                            }
                        }

                        int timeExtra = elapsedTime - usageGoal;
                        usageGoalLabel.text = "Congratulations! You have worked " + SmallStringTime(timeExtra) + " more than your goal today.";
                    }
                }
            }
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

        private string SmallStringTime(int time)
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