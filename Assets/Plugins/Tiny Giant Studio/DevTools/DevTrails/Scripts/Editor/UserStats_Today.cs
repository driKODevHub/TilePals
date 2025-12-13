using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace TinyGiantStudio.DevTools.DevTrails
{
    /// <summary>
    /// TODO user stats for global and project
    /// </summary>
    [FilePath("Tiny Giant Studio/UserStats_today.asset", FilePathAttribute.Location.PreferencesFolder)]
    public class UserStats_Today : ScriptableSingleton<UserStats_Today>
    {
        #region Variables

        [SerializeField] private TGSTime currentDate; //Note to self: Date time isn't serializable

        public void VerifyNewDay(bool saveChange)
        {
            if (currentDate == null)
                currentDate = new TGSTime(DateTime.Now);

            if (!currentDate.IsToday())
            {
                UserStats_Global.instance.NewDayHappenedInTheMiddleOfSession();
                showedUsagePopUpToday = false;
                //Debug.Log("New day detected. Resetting all counter for today. Is " + DateTime.Now.Date + " and saved in " + currentDate.GetDate());
                currentDate = new TGSTime(DateTime.Now);
                Reset(saveChange);
            }
        }

        public bool showedUsagePopUpToday = false;

        //Will implement later
        //[SerializeField] private int _totalUseTime;

        //public int TotalUseTime
        //{
        //    get
        //    {
        //        return _totalUseTime;
        //    }
        //    set
        //    {
        //        VerifyNewDay();

        //        DateTime now = DateTime.Now;
        //        TimeSpan timeSinceMidnight = now - now.Date;
        //        var millisecondsSinceMidnight = (int)timeSinceMidnight.TotalMilliseconds;

        //        if (millisecondsSinceMidnight > value)
        //            _totalUseTime = value;
        //        else
        //            _totalUseTime = value - millisecondsSinceMidnight;

        //                    SaveToDisk();
        //    }
        //}

        [SerializeField] private int _enteredPlayMode;

        public int EnteredPlayMode
        {
            get
            {
                return _enteredPlayMode;
            }
            set
            {
                VerifyNewDay(false);
                _enteredPlayMode = value;
                SaveToDisk();
            }
        }

        [SerializeField] private int _playModeUseTime;

        public int PlayModeUseTime
        {
            get
            {
                return _playModeUseTime;
            }
            set
            {
                _playModeUseTime = value;
                SaveToDisk();
            }
        }

        [SerializeField] private int _sceneSaved;

        public int SceneSaved
        {
            get
            {
                return _sceneSaved;
            }
            set
            {
                VerifyNewDay(false);
                _sceneSaved = value;
                SaveToDisk();
            }
        }

        [SerializeField] private int _sceneOpened;

        public int SceneOpened
        {
            get
            {
                return _sceneOpened;
            }
            set
            {
                VerifyNewDay(false);
                _sceneOpened = value;
                SaveToDisk();
            }
        }

        [SerializeField] private int _sceneClosed;

        public int SceneClosed
        {
            get
            {
                return _sceneClosed;
            }
            set
            {
                VerifyNewDay(false);
                _sceneClosed = value;
                SaveToDisk();
            }
        }

        [SerializeField] private int _undoRedoCounter;

        public int UndoRedoCounter
        {
            get
            {
                return _undoRedoCounter;
            }
            set
            {
                VerifyNewDay(false);
                _undoRedoCounter = value;
                SaveToDisk();
            }
        }

        [SerializeField] private int _compileCounter;

        public int CompileCounter
        {
            get
            {
                return _compileCounter;
            }
            set
            {
                VerifyNewDay(false);
                _compileCounter = value;
                SaveToDisk();
            }
        }

        [SerializeField] private int _totalTimeSpentCompiling;

        public int TotalTimeSpentCompiling
        {
            get
            {
                return _totalTimeSpentCompiling;
            }
            set
            {
                VerifyNewDay(false);
                _totalTimeSpentCompiling = value;
                SaveToDisk();
            }
        }
        [SerializeField] private float _timeSpentCompiling;

        public float TimeSpentCompiling
        {
            get
            {
                return _timeSpentCompiling;
            }
            set
            {
                VerifyNewDay(false);
                _timeSpentCompiling = value;
                SaveToDisk();
            }
        }

        [SerializeField] int _totalTimeSpentInDomainReload;

        public int TotalTimeSpentInDomainReload
        {
            get => _totalTimeSpentInDomainReload;
            set
            {
                VerifyNewDay(false);
                _totalTimeSpentInDomainReload = value;
                SaveToDisk();
            }
        }
        
        [SerializeField] float timeSpentInDomainReload;
        public float TimeSpentInDomainReload
        {
            get => timeSpentInDomainReload;
            set
            {
                VerifyNewDay(false);
                timeSpentInDomainReload = value;
                SaveToDisk();
            }
        }

        [SerializeField] private int _probableCrashes;

        public int ProbableCrashes
        {
            get => _probableCrashes;
            set
            {
                VerifyNewDay(false);
                _probableCrashes = value;
                SaveToDisk();
            }
        }
        [SerializeField] private int _projectOpenCounter;

        public int ProjectOpenCounter
        {
            get => _projectOpenCounter;
            set
            {
                VerifyNewDay(false);
                _projectOpenCounter = value;
                SaveToDisk();
            }
        }

        [SerializeField] private int _totalTimeSpentOpeningProject;

        public int TotalTimeSpentOpeningProject
        {
            get => _totalTimeSpentOpeningProject;
            set
            {
                VerifyNewDay(false);
                _totalTimeSpentOpeningProject = value;
                SaveToDisk();
            }
        }
        #endregion Variables

        #region Console Logs

        [SerializeField] private int _logCounter_editor;

        public int LogCounter_editor
        {
            get
            {
                return _logCounter_editor;
            }
            set
            {
                VerifyNewDay(false);
                _logCounter_editor = value;
                SaveToDisk();
            }
        }

        [SerializeField] private int _logCounter_playMode;

        public int LogCounter_playMode
        {
            get
            {
                return _logCounter_playMode;
            }
            set
            {
                VerifyNewDay(false);
                _logCounter_playMode = value;
                SaveToDisk();
            }
        }

        [SerializeField] private int _warningLogCounter_editor;

        public int WarningLogCounter_editor
        {
            get
            {
                return _warningLogCounter_editor;
            }
            set
            {
                VerifyNewDay(false);
                _warningLogCounter_editor = value;
                SaveToDisk();
            }
        }

        [SerializeField] private int _warningLogCounter_playMode;

        public int WarningLogCounter_playMode
        {
            get
            {
                return _warningLogCounter_playMode;
            }
            set
            {
                VerifyNewDay(false);
                _warningLogCounter_playMode = value;
                SaveToDisk();
            }
        }

        [SerializeField] private int _errorLogCounter_editor;

        public int ErrorLogCounter_editor
        {
            get
            {
                return _errorLogCounter_editor;
            }
            set
            {
                VerifyNewDay(false);
                _errorLogCounter_editor = value;
                SaveToDisk();
            }
        }

        [SerializeField] private int _errorLogCounter_playMode;

        public int ErrorLogCounter_playMode
        {
            get
            {
                return _errorLogCounter_playMode;
            }
            set
            {
                VerifyNewDay(false);
                _errorLogCounter_playMode = value;
                SaveToDisk();
            }
        }

        [SerializeField] private int _exceptionLogCounter_editor;

        public int ExceptionLogCounter_editor
        {
            get
            {
                return _exceptionLogCounter_editor;
            }
            set
            {
                VerifyNewDay(false);
                _exceptionLogCounter_editor = value;
                SaveToDisk();
            }
        }

        [SerializeField] private int _exceptionLogCounter_playMode;

        public int ExceptionLogCounter_playMode
        {
            get
            {
                return _exceptionLogCounter_playMode;
            }
            set
            {
                VerifyNewDay(false);
                _exceptionLogCounter_playMode = value;
                SaveToDisk();
            }
        }

        #endregion Console Logs

        [SerializeField] private int _devToolsEditorWindowOpened;

        public int DevToolsEditorWindowOpened
        {
            get
            {
                return _devToolsEditorWindowOpened;
            }
            set
            {
                VerifyNewDay(false);
                _devToolsEditorWindowOpened = value;
                SaveToDisk();
            }
        }

        #region Methods

        public void Reset(bool saveChange)
        {
            _enteredPlayMode = 0;
            _playModeUseTime = 0;
            _sceneSaved = 0;
            _sceneOpened = 0;
            _sceneClosed = 0;
            _undoRedoCounter = 0;
            _compileCounter = 0;
            _totalTimeSpentCompiling = 0;
            _timeSpentCompiling = 0;
            _totalTimeSpentInDomainReload = 0;
            timeSpentInDomainReload = 0;

            _logCounter_editor = 0;
            _logCounter_playMode = 0;
            _warningLogCounter_editor = 0;
            _warningLogCounter_playMode = 0;
            _exceptionLogCounter_editor = 0;
            _exceptionLogCounter_playMode = 0;
            _errorLogCounter_editor = 0;
            _errorLogCounter_playMode = 0;

            _devToolsEditorWindowOpened = 0;
            _probableCrashes = 0;

            if (saveChange)
                SaveToDisk();
        }

        // Define your file path (My Documents)
        private static string GetCustomSavePath()
        {
#if UNITY_EDITOR_WIN
            string myPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
#elif UNITY_EDITOR_OSX
            string myPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
#elif UNITY_EDITOR_LINUX
            string myPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
#else
            throw new System.NotSupportedException("Unsupported platform");
#endif
            return Path.Combine(myPath, "Tiny Giant Studio/DevTrails", "Today's Stats.json");
        }

        // Load from file
        public void LoadFromDisk()
        {
            string path = GetCustomSavePath();
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                JsonUtility.FromJsonOverwrite(json, this);
                //Debug.Log("Loaded Stats from {path}");
            }
            else
            {
                //Debug.Log("No existing Stats found at {path}, using defaults.");
            }
        }

        //// Save to file
        //public void SaveToDisk()
        //{
        //    try
        //    {
        //        string path = GetCustomSavePath();
        //        if (string.IsNullOrWhiteSpace(path))
        //            throw new Exception("Invalid or empty path.");

        //        string directory = Path.GetDirectoryName(path);
        //        if (!string.IsNullOrEmpty(directory))
        //            Directory.CreateDirectory(directory);

        //        string json = JsonUtility.ToJson(this, true);
        //        File.WriteAllText(path, json);

        //        //Debug.Log($"Settings saved to {path}");
        //    }
        //    catch (UnauthorizedAccessException e)
        //    {
        //        Debug.LogError($"Couldn't save stats. Access denied: {e.Message}");
        //    }
        //    catch (IOException e)
        //    {
        //        Debug.LogError($"Couldn't save stats. IO error while saving: {e.Message}");
        //    }
        //    catch (Exception e)
        //    {
        //        Debug.LogError($"Couldn't save stats. Unexpected error: {e.Message}");
        //    }
        //}

        private static readonly object fileLock = new object();

        // Save to file
        public void SaveToDisk()
        {
            try
            {
                string path = GetCustomSavePath();
                if (string.IsNullOrWhiteSpace(path))
                    throw new Exception("Invalid or empty path.");

                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                string json = JsonUtility.ToJson(this, true);

                //File.WriteAllText(path, json);

                // Attempt to write with retry mechanism
                const int maxAttempts = 3;
                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        lock (fileLock)
                        {
                            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                            using (var writer = new StreamWriter(stream))
                            {
                                writer.Write(json);
                            }
                        }

                        // Success � break out of retry loop
                        break;
                    }
                    //catch (IOException e) when (attempt < maxAttempts)
                    catch (IOException) when (attempt < maxAttempts)
                    {
                        // If file is in use, wait and retry
                        System.Threading.Thread.Sleep(100); // Wait 100ms before retrying
                        continue;
                    }
                }


                //Debug.Log($"Settings saved to {path}");
            }
            catch (UnauthorizedAccessException e)
            {
                Debug.LogError($"Couldn't save stats. Access denied: {e.Message}");
            }
            catch (IOException e)
            {
                Debug.LogError($"Couldn't save stats. IO error while saving: {e.Message}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Couldn't save stats. Unexpected error: {e.Message}");
            }
        }

        private void Save()
        {
            //Save(true);
            //SaveToDisk();
        }

        #endregion Methods
    }
}