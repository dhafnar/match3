using System;
using UnityEngine;


//using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;
using Firebase.Firestore;
using Firebase.Crashlytics;
using Object = UnityEngine.Object;
using System.Linq;
using System.Net.Http;
using DefaultNamespace;
using UnityEngine.Diagnostics;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;


// the GameManager is the master controller for the GamePlay

[RequireComponent(typeof(LevelGoal))]
public class GameManager : Singleton<GameManager>
{
    public World[] availableWorlds;
    public static int numBoostersUsed;
    public List<JsonMoveData> jsonListOfDataForMoves = new();

	// SPECIFY YOUR GOOGLE CLOUD FUNCTIONS HERE
    private static String levelDataUrl = "https://#########.cloudfunctions.net/levelStatsEndpoint";
    private static String analyticsUrl = "http://#########.cloudfunctions.net/analyticsEndpoint";
    private static String triggerLevelsRefreshUrl =
        "https://#########.cloudfunctions.net/genDefaultLevelsEndpoint";

	// SPECIFY YOUR GOOGLE CLOUD STORAGE BUCKET HERE
    private static String bucketJsonDataUrl = "https://storage.googleapis.com/#########/";

		
    private static World currentWorld;
    public static int currentLevel = 0;
    public static int levelsAvailableOverall = 0;
    public static int levelWithinSameArray = 0;
    public static int timesDefaultLevelsServed = -1;
    private static bool isFirebaseReady = false;
    private static bool justCompletedALevel;
    private static bool buttonExitsGame;
    private static bool buttonConfirmsInstructionsViewed = false;
    private static bool instructionsWereShown = false;
    private static bool resetTestGroup;
    private static int bundleVersion;
    private static String userIdKey = "USER_ID";
    private static String currentLevelNumName = "CURRENT_LEVEL";
    private static String currentLevelUidName = "CURRENT_LEVEL_UID";
    private static String sameArraycurrentLevelNumName = "CURRENT_LEVEL_SAME_ARRAY";
    private static String levelsServingStrategyKey = "SERVING_STRATEGY";
    private static String levelsServingStrategy = "";
    private static String firestoreLevels = "levels";
    private static String firestoreUsers = "users";
    private static String common = "common";
    private static String userId = "";
    private static String deviceModel;
    private static String lastPlayedLevelUid;
    private FirebaseFirestore firestoreInstance;
    public DateTime startTime;


    private double timePlaying;
    private string levelKey;
    public static string levelGuid;

    private LevelInputsJson[] levelInputsArray;

    // Saves the data returned from the initial backend call that
    // determines the category of the player
    private string jsonFromInitialCall;

    // count the missclicks
    public static int numFailedMoves = 0;

    // count any clicks on the board
    public static int numBoardClicksOverall = 0;

    // reference to the Board
    Board m_board;

    // is the player read to play?
    bool m_isReadyToBegin = false;

    // is the game over?
    bool m_isGameOver = false;


    public bool IsGameOver
    {
        get { return m_isGameOver; }
        set { m_isGameOver = value; }
    }

    // do we have a winner?
    bool m_isWinner = false;

    // are we ready to load/reload a new level?
    bool m_isReadyToReload = false;

    // reference to LevelGoal component
    LevelGoal m_levelGoal;

    LevelGoalCollected m_levelGoalCollected;

    // public reference to LevelGoalTimed component
    public LevelGoal LevelGoal
    {
        get { return m_levelGoal; }
    }

    // Should debug settings be shown
    public bool debug = true;

    // dh - a class to hold data for individual moves
    public class JsonMoveData
    {
        public int moveNumber { get; }

        public string createTime { get; }
        public double durationInSeconds { get; }
        public bool isMoveLegal { get; }
        public int scoreForMove { get; }
        public int starsForMove { get; }
        public string swipeDirection { get; }

        public JsonMoveData(int order, DateTime moveCreateTime, double durInSeconds, bool moveWasLegal,
            int scoreForTheMove, int starsForTheMove, string swipeDirStr)
        {
            moveNumber = order;
            createTime = moveCreateTime.ToString("yyyy-MM-dd HH:mm:ss");
            durationInSeconds = durInSeconds;
            isMoveLegal = moveWasLegal;
            scoreForMove = scoreForTheMove;
            starsForMove = starsForTheMove;
            swipeDirection = swipeDirStr;
        }
    }

    public override void Awake()
    {
        base.Awake();

        // Get device model for statistics
        deviceModel = SystemInfo.deviceModel;

        // fill in LevelGoal and LevelGoalTimed components
        m_levelGoal = GetComponent<LevelGoal>();
//        m_levelGoalTimed = GetComponent<LevelGoalTimed>();

        m_levelGoalCollected = GetComponent<LevelGoalCollected>();

        // cache a reference to the Board
        m_board = Object.FindObjectOfType<Board>().GetComponent<Board>();
    }


    // looks at a specific world and reads the data
    void ConfigureLevel(int levelIndex)
    {
        
        if (currentWorld == null)
        {
            Debug.LogError("GAMEMANAGER SetupLevelData: missing world...");
            return;
        }
        
        Debug.Log("levelInputsArray.Length: " + levelInputsArray.Length);

        if (levelIndex >= levelInputsArray.Length)
        {
            Debug.LogError("GAMEMANAGER SetupLevelData: invalid level index ...");
            FirebaseAnalytics.LogEvent(
                "error",
                new Parameter("type", "InvalidLevelIndex"),
                new Parameter("message", "Invalid level index")
            );

            levelIndex = 0; // If level doesn't exist, go to back to the start
            levelWithinSameArray = 0;
//            return;
        }

        if (m_board == null)
        {
            Debug.LogError("GAMEMANAGER SetupLevelData: missing Board ...");
            return;
        }
        // Create a level based on the JSON retrieved from the bucket and the current level of the user
        Level levelConfig = LevelGenerator.Generate(levelInputsArray[levelIndex]);

        // Save UID so it isn't loaded again
        lastPlayedLevelUid = levelInputsArray[levelIndex].LevelGuid;

        // Use the LevelParams object to generate a Level
        m_board.width = levelConfig.width;
        m_board.height = levelConfig.height;
        m_board.startingTiles = levelConfig.startingTiles;
        m_board.startingGamePieces = levelConfig.startingGamePieces;
        m_board.startingBlockers = levelConfig.startingBlockers;
        m_board.chanceForCollectible = levelConfig.chanceForCollectible;
        m_board.gamePiecePrefabs = levelConfig.gamePiecePrefabs;

        // we need to create a new Collection Goal array by instantiating the prefabs
        List<CollectionGoal> goals = new List<CollectionGoal>();
        foreach (CollectionGoal g in levelConfig.collectionGoals)
        {
            CollectionGoal instance = Instantiate(g, transform);
            goals.Add(instance);
        }

        // we can only assign the array of instances to the 
        m_levelGoalCollected.collectionGoals = goals.ToArray();
        m_levelGoalCollected.scoreGoals = levelConfig.scoreGoals;
        m_levelGoalCollected.movesLeft = levelConfig.movesLeft;
        m_levelGoalCollected.timeLeft = levelConfig.timeLeft;
        m_levelGoalCollected.levelCounter = levelConfig.levelCounter;
    }

    IEnumerator Start()
    {
        bundleVersion = getBundleVersion();
        
        // Hide elements until needed - visual improvement only
        UIManager.Instance.levelNameText.enabled = false;
        UIManager.Instance.messageWindow.gameObject.SetActive(false);

        PrepareUserIdAndTriggerInstructions();

        // Set default world
        if (currentWorld == null)
        {
            currentWorld = availableWorlds[0]; // Normal world
        }

        Debug.Log("levelsServingStrategy:" + levelsServingStrategy);

        // reset the number of failed moves
        numFailedMoves = 0;

        // Asynchronously retrieve levels from Firestore
        var task = ProcessJsonDataFromBucketAsync();
        yield return new WaitUntil(() => task.IsCompleted);

        levelInputsArray = task.Result;

        // Get values from playerpresf
        currentLevel = ProvideCurrentLevelNum(currentLevelNumName);

        if (PlayerPrefs.HasKey(currentLevelUidName))
        {
            lastPlayedLevelUid = PlayerPrefs.GetString(currentLevelUidName);
        }
        else
        {
            lastPlayedLevelUid = "";
        }

        // If this level has been beaten before, skip to the next one
        // Check against all levelGuids in the array
        levelWithinSameArray = 0;
        for (int i = 0; i < levelInputsArray.Length; i++)
        {
            if (levelInputsArray[i].LevelGuid == lastPlayedLevelUid)
            {
                // Set it to one after the one just played
                levelWithinSameArray = i + 1;
            }
        }

        // If we are still in default levels, it should move forward regardless of id
        if (timesDefaultLevelsServed > 0)
        {
            levelWithinSameArray = timesDefaultLevelsServed;
        }
        
        Debug.Log("levelWithinSameArray: " + levelWithinSameArray);

        // Select the appropriate level in the array
        ConfigureLevel(levelWithinSameArray);

        // This many levels are still available: all - however many were already played
        // levelWithinSameArray is 0 if levels are fresh and increases by one for any finished level
        levelsAvailableOverall = levelInputsArray.Length - levelWithinSameArray;

        if (UIManager.Instance != null)
        {
            // position ScoreStar horizontally
            if (UIManager.Instance.scoreMeter != null)
            {
                UIManager.Instance.scoreMeter.SetupStars(m_levelGoal);
            }

            // use the Scene name as the Level name
            if (UIManager.Instance.levelNameText != null)
            {
                // get a reference to the current Scene
                // Scene scene = SceneManager.GetActiveScene();
                // UIManager.Instance.levelNameText.text = scene.name;
                UIManager.Instance.levelNameText.text =
                    "Level " + (currentLevel + 1); // dh +1 to make it more human readable
            }

            if (m_levelGoalCollected != null)
            {
                UIManager.Instance.EnableCollectionGoalLayout(true);
                UIManager.Instance.SetupCollectionGoalLayout(m_levelGoalCollected.collectionGoals);
            }
            else
            {
                UIManager.Instance.EnableCollectionGoalLayout(false);
            }

            // count the time for analytical reasons
            startTime = DateTime.Now;

            bool useTimer = (m_levelGoal.levelCounter == LevelCounter.Timer);

            UIManager.Instance.EnableTimer(useTimer);
            UIManager.Instance.EnableMovesCounter(!useTimer);
/*
            if (justCompletedALevel)
            {
                AskIfTheyLikedTheLevel();
            }
            */
        }

        // update the moves left UI
        m_levelGoal.movesLeft++;
        UpdateMoves();

        // start the main game loop
        StartCoroutine("ExecuteGameLoop");
    }


    // Either loads or generates and saves an unique user id
    private void PrepareUserIdAndTriggerInstructions()
    {
        if (PlayerPrefs.HasKey(userIdKey))
        {
            userId = PlayerPrefs.GetString(userIdKey);
            Debug.Log($"User ID loaded: {userId}");
            // No need to trigger instruction window
            buttonConfirmsInstructionsViewed = false; //should be false
        }

        else
        {
            userId = "UID-" + Guid.NewGuid().ToString();
            PlayerPrefs.SetString(userIdKey, userId);
            PlayerPrefs.Save();
            Debug.Log($"User ID generated: {userId}");
            // Game opened first time, trigger instruction window
            buttonConfirmsInstructionsViewed = true;
        }

        if (PlayerPrefs.HasKey(levelsServingStrategyKey))
        {
            levelsServingStrategy = PlayerPrefs.GetString(levelsServingStrategyKey);
        }
        else
        {
            levelsServingStrategy = AssignWorldServed();
            PlayerPrefs.SetString(levelsServingStrategyKey, levelsServingStrategy);
            PlayerPrefs.Save();
        }
    }

    // Either loads or generates and saves an unique user id
    private int ProvideCurrentLevelNum(String keyName)
    {
        int _currentLevel;
        if (PlayerPrefs.HasKey(keyName))
        {
            _currentLevel = PlayerPrefs.GetInt(keyName);
        }

        else
        {
            _currentLevel = 0;
        }

        return _currentLevel;
    }

    // update the Text component that shows our moves left
    public void UpdateMoves()
    {
        // if the LevelGoal is not timed (e.g. LevelGoalScored)...
        if (m_levelGoal.levelCounter == LevelCounter.Moves)
        {
            // decrement a move
            m_levelGoal.movesLeft--;

            // update the UI
            if (UIManager.Instance != null && UIManager.Instance.movesLeftText != null)
            {
                UIManager.Instance.movesLeftText.text = m_levelGoal.movesLeft.ToString();
            }
        }
    }

    // this is the main coroutine for the Game, that determines are basic beginning/middle/end

    // each stage of the game must complete before we advance to the next stage
    // add as many stages here as necessary

    IEnumerator ExecuteGameLoop()
    {
        yield return StartCoroutine("StartGameRoutine");
        yield return StartCoroutine("PlayGameRoutine");

        // wait for board to refill
        yield return StartCoroutine("WaitForBoardRoutine", 0.5f);

        yield return StartCoroutine("EndGameRoutine");
    }

    public void ButtonPressed()
    {
        /*
        LevelStatsToCloudFunction(m_isWinner,
            m_levelGoal.movesLeft,
            ScoreManager.Instance.CurrentScore, levelKey, timePlaying, null);
        */
        if (buttonExitsGame)
        {
            Application.Quit();
        }
        else if (buttonConfirmsInstructionsViewed)
        {
            buttonConfirmsInstructionsViewed = false;
            BeginGame();
        }
        else

        {
            BeginGame();
        }
    }

    // switches ready to begin status to true
    public void BeginGame()
    {
        UIManager.Instance.levelNameText.enabled = true;
        m_isReadyToBegin = true;
    }

    // coroutine for the level introduction
    IEnumerator StartGameRoutine()
    {
        if (UIManager.Instance != null)
        {
            if (buttonExitsGame)
            {
                //             determineTestGroup(); // Determines once again what group the player should belong to
                ShowEndGameScreen();
            }

            else if (buttonConfirmsInstructionsViewed)
            {
                ShowInstructionScreen();
            }

            else
            {
                ShowStartGameWindow();
            }
        }

        // wait until the player is ready
        while (!m_isReadyToBegin)
        {
            yield return null;
        }

        // fade off the ScreenFader
        if (UIManager.Instance != null && UIManager.Instance.screenFader != null)
        {
            UIManager.Instance.screenFader.FadeOff();
        }

        // wait half a second
        yield return new WaitForSeconds(0.5f);

        // setup the Board
        if (m_board != null)
        {
            m_board.boardSetup.SetupBoard();
        }
    }

    // coroutine for game play
    IEnumerator PlayGameRoutine()
    {
        FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelStart,
            new Parameter(FirebaseAnalytics.ParameterLevel, currentLevel),
            getUserIdParam(),
            getlevelServingStrategy(),
            getGameVersion());

        // if level is timed, start the timer
        if (m_levelGoal.levelCounter == LevelCounter.Timer)
        {
            m_levelGoal.StartCountdown();
        }

        // while the end game condition is not true, we keep playing
        // just keep waiting one frame and checking for game conditions
        while (!m_isGameOver)
        {
            m_isGameOver = m_levelGoal.IsGameOver();
            m_isWinner = m_levelGoal.IsWinner();

            // wait one frame
            yield return null;
        }
    }

    IEnumerator WaitForBoardRoutine(float delay = 0f)
    {
        if (m_levelGoal.levelCounter == LevelCounter.Timer && UIManager.Instance != null
                                                           && UIManager.Instance.timer != null)
        {
            UIManager.Instance.timer.FadeOff();
            UIManager.Instance.timer.paused = true;
        }

        if (m_board != null)
        {
            // this accounts for the swapTime delay in the Board's SwitchTilesRoutine BEFORE ClearAndRefillRoutine is invoked
            yield return new WaitForSeconds(m_board.swapTime);

            // wait while the Board is refilling
            while (m_board.isRefilling)
            {
                yield return null;
            }
        }

        // extra delay before we go to the EndGameRoutine
        yield return new WaitForSeconds(delay);
    }

    // coroutine for the end of the level
    IEnumerator EndGameRoutine()
    {
        // set ready to reload to false to give the player time to read the screen
        m_isReadyToReload = false;
        levelKey = "numFailsLevel_" + currentLevel.ToString();

        // calculate the time playing
        var ts = (DateTime.Now - startTime);
        timePlaying = Math.Round(ts.TotalSeconds, 1);

        currentWorld = availableWorlds[0]; // Normal World

        // if player beat the level goals, show the win screen and play the win sound
        if (m_isWinner)
        {
            IncreaseLevel(m_isWinner);
            PlayerPrefs.SetInt(levelKey, PlayerPrefs.GetInt(levelKey) + 1);

            FirebaseAnalytics.LogEvent(
                FirebaseAnalytics.EventLevelUp,
                new Parameter(
                    FirebaseAnalytics.ParameterLevel, currentLevel),
                new Parameter(FirebaseAnalytics.ParameterSuccess,
                    "Passed"),
                getUserIdParam(),
                getlevelServingStrategy(),
                getGameVersion()
            );
            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelEnd, getUserIdParam());

            ShowWinScreen();
        }
        // otherwise, show the lose screen and play the lose sound
        else
        {
            logScreen("LostScreen");
            ShowLoseScreen();
        }
        /*
        LevelStatsToCloudFunction(m_isWinner,
            m_levelGoal.movesLeft,
            ScoreManager.Instance.CurrentScore, levelKey, timePlaying, null);
        */

        // wait one second
        yield return new WaitForSeconds(1f);

        // fade the screen 
        if (UIManager.Instance != null && UIManager.Instance.screenFader != null)
        {
            UIManager.Instance.screenFader.FadeOn();
        }

        // wait until read to reload
        while (!m_isReadyToReload)
        {
            yield return null;
        }

        // reload the scene (you would customize this to go back to the menu or go to the next level
        // but we just reload the same scene in this demo
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void ShowInstructionScreen()
    {
        if (UIManager.Instance != null && UIManager.Instance.messageWindow != null)
        {
            logScreen("InstructionScreen");
            UIManager.Instance.messageWindow.gameObject.SetActive(true);
            UIManager.Instance.messageWindow.GetComponent<RectXformMover>().MoveOn();
            UIManager.Instance.messageWindow.ShowInstructionsMessage();
            UIManager.Instance.messageWindow.ShowCollectionGoal(false);
            string caption = "Swap nearby \npieces to match \nthree of the \nsame color.";
            UIManager.Instance.messageWindow.ShowGoalCaption(caption, 0, 70);

            if (UIManager.Instance.messageWindow.goalFailedIcon != null)
            {
                UIManager.Instance.messageWindow.ShowGoalImage(UIManager.Instance.messageWindow.movesIcon);
            }
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayWinSound();
        }
    }


    void ShowWinScreen()
    {
        if (UIManager.Instance != null && UIManager.Instance.messageWindow != null)
        {
            logScreen("WinScreen");
            UIManager.Instance.messageWindow.gameObject.SetActive(true);
            UIManager.Instance.messageWindow.GetComponent<RectXformMover>().MoveOn();
            UIManager.Instance.messageWindow.ShowWinMessage();
            UIManager.Instance.messageWindow.ShowCollectionGoal(false);

            if (ScoreManager.Instance != null)
            {
                string scoreStr = "you scored\n" + ScoreManager.Instance.CurrentScore.ToString() + " points!";
                UIManager.Instance.messageWindow.ShowGoalCaption(scoreStr, 0, 70);
            }

            if (UIManager.Instance.messageWindow.goalCompleteIcon != null)
            {
                UIManager.Instance.messageWindow.ShowGoalImage(UIManager.Instance.messageWindow.goalCompleteIcon);
            }
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayWinSound();
        }

        justCompletedALevel = true;
    }

    void ShowEndGameScreen()
    {
        if (UIManager.Instance != null && UIManager.Instance.messageWindow != null)
        {
            logScreen("EndGameScreen");
            UIManager.Instance.messageWindow.gameObject.SetActive(true);
            UIManager.Instance.messageWindow.GetComponent<RectXformMover>().MoveOn();
            UIManager.Instance.messageWindow.ShowEndGameMessage();
            UIManager.Instance.messageWindow.ShowCollectionGoal(false);

            string caption = "Thank you \nfor playing!";

            UIManager.Instance.messageWindow.ShowGoalCaption(caption, 0, 70);

            if (UIManager.Instance.messageWindow.goalFailedIcon != null)
            {
                UIManager.Instance.messageWindow.ShowGoalImage(UIManager.Instance.messageWindow.goalCompleteIcon);
            }
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayWinSound();
        }
    }

    void AskIfTheyLikedTheLevel()
    {
        ShowLoseScreen();

        justCompletedALevel = false;
    }

    void ShowLoseScreen()
    {
        if (UIManager.Instance != null && UIManager.Instance.messageWindow != null)
        {
            UIManager.Instance.messageWindow.gameObject.SetActive(true);
            UIManager.Instance.messageWindow.GetComponent<RectXformMover>().MoveOn();
            UIManager.Instance.messageWindow.ShowLoseMessage();
            UIManager.Instance.messageWindow.ShowCollectionGoal(false);

            string caption = "";
            if (m_levelGoal.levelCounter == LevelCounter.Timer)
            {
                caption = "Out of time!";
            }
            else
            {
                caption = "Out of moves!";
            }

            UIManager.Instance.messageWindow.ShowGoalCaption(caption, 0, 70);

            if (UIManager.Instance.messageWindow.goalFailedIcon != null)
            {
                UIManager.Instance.messageWindow.ShowGoalImage(UIManager.Instance.messageWindow.goalFailedIcon);
            }
        }
/*
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayLoseSound();
        }
        */
    }

    // use this to acknowledge that the player is ready to reload
    public void ReloadScene()
    {
        m_isReadyToReload = true;
    }

    // score points and play a sound
    public void ScorePoints(GamePiece piece, int multiplier = 1, int bonus = 0)
    {
        if (piece != null)
        {
            if (ScoreManager.Instance != null)
            {
                // score points
                ScoreManager.Instance.AddScore(piece.scoreValue * multiplier + bonus);

                // update the scoreStars in the Level Goal component
                m_levelGoal.UpdateScoreStars(ScoreManager.Instance.CurrentScore);

                if (UIManager.Instance != null && UIManager.Instance.scoreMeter != null)
                {
                    UIManager.Instance.scoreMeter.UpdateScoreMeter(ScoreManager.Instance.CurrentScore,
                        m_levelGoal.scoreStars);
                }
            }

            // play scoring sound clip
            if (SoundManager.Instance != null && piece.clearSound != null)
            {
                SoundManager.Instance.PlayClipAtPoint(piece.clearSound, Vector3.zero, SoundManager.Instance.fxVolume);
            }
        }
    }

    public void AddTime(int timeValue)
    {
        if (m_levelGoal.levelCounter == LevelCounter.Timer)
        {
            m_levelGoal.AddTime(timeValue);
        }
    }

    public void UpdateCollectionGoals(GamePiece pieceToCheck)
    {
        if (m_levelGoalCollected != null)
        {
            m_levelGoalCollected.UpdateGoals(pieceToCheck);
        }
    }


    public async Task<LevelInputsJson[]> ProcessJsonDataFromBucketAsync()
    {
        using (var client = new HttpClient())
        {
            var fileUrl = bucketJsonDataUrl + userId + ".json";

            Debug.Log("fileUrl:" + fileUrl);

            string response;

            try
            {
                // Sends an HTTP request to the specified URL
                response = await client.GetStringAsync(fileUrl);
                // Using this to signal that proper levels were loaded
                timesDefaultLevelsServed = -1;
            }
            catch (Exception)
            {
                // Using this to know to move to the next level
                timesDefaultLevelsServed += 1;

                // If the file doesn't exist, go to serving strategy's default levels
                if (levelsServingStrategy == "random")
                    fileUrl = bucketJsonDataUrl + "000_random_default.json";
                else
                    fileUrl = bucketJsonDataUrl + "000_gpt_default.json";

                response = await client.GetStringAsync(fileUrl);

                var dictForLevelsReset = new
                {
                    levelsServingStrategy
                };

                string jsonForLevelsReset = JsonConvert.SerializeObject(dictForLevelsReset);
                StartCoroutine(postRequest(triggerLevelsRefreshUrl, jsonForLevelsReset));
            }

            // Parses the response (as a JSON array)
            var jsonArray = JArray.Parse(response);

            string jsonString = JsonConvert.SerializeObject(jsonArray);

            Debug.Log(jsonString);

            LevelInputsJson[] levels = JsonConvert.DeserializeObject<LevelInputsJson[]>(jsonString);

            return levels;
        }
    }


/*
    async Task<LevelInputsJson[]> ReadFromFirestore()
    {
        FirebaseFirestore firestoreInstance = FirebaseFirestore.DefaultInstance;

        DocumentReference docRef = firestoreInstance.Collection(firestoreUsers).Document(userId);

        DocumentSnapshot snapshot = await docRef.GetSnapshotAsync();

        if (snapshot.Exists)
        {
            Dictionary<string, object> levelObjects = snapshot.ToDictionary();

            Debug.Log(levelObjects);

            string jsonString = JsonConvert.SerializeObject(levelObjects);

            LevelInputsJson[] levels = JsonConvert.DeserializeObject<LevelInputsJson[]>(jsonString);

            return levels;
        }
        else
        {
            Console.WriteLine("Document does not exist!");
        }

        return null;
    }
*/
    // dh here I send the data to the cloud
    private void LevelStatsToCloudFunction(bool isWinner, int movesLeft, int score, String ppLevelKey,
        Double timePlaying, int? userRating)
    {
        var dataForLevel = new
        {
            userId,
            currentLevel,
            levelGuid,
            numTimesLost = PlayerPrefs.GetInt(ppLevelKey),
            isLevelPassed = isWinner,
            deviceModel,
            score,
            numBoardClicksOverall,
            movesLeft,
            timePlaying,
            numFailedMoves,
            dateSent = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            userRating,
            worldServed = levelsServingStrategy,
            numBoostersUsed = numBoostersUsed > 0 ? 1 : 0,
            gameVersion = bundleVersion
        };

        var levelPlusMovesData = new
        {
            level = dataForLevel,
            moves = jsonListOfDataForMoves
        };
        //    string jsonData = JsonConvert.SerializeObject(dataForLevel); 
        string jsonLevelPlusMovesData = JsonConvert.SerializeObject(levelPlusMovesData);
        Debug.Log("Data sent to backend: " + jsonLevelPlusMovesData);
        if (numBoardClicksOverall > 0) // only send if the user has played the level - not if debugging
        {
            StartCoroutine(postRequest(levelDataUrl, jsonLevelPlusMovesData));
        }
    }


    class DataFromInitialCall
    {
        public bool isEven;
    }


    private void IncreaseLevel(bool isWinner)
    {
        if (isWinner) // Increase level only if the user won
        {
            // Increase only if there are more levels
            if (levelInputsArray != null && levelsAvailableOverall > 0) // todo change back if needed
            {
                currentLevel += 1;
                PlayerPrefs.SetString(currentLevelUidName, lastPlayedLevelUid);
            }
            else
            {
                currentLevel = 0;
                buttonExitsGame = true; // If there are no more levels, exit the game
            }

            PlayerPrefs.SetInt(currentLevelNumName, currentLevel);
            PlayerPrefs.Save();
        }
    }


    void ShowStartGameWindow()
    {
        // show the message window with the level goal
        if (UIManager.Instance.messageWindow != null)
        {
            UIManager.Instance.messageWindow.gameObject.SetActive(true);
            UIManager.Instance.messageWindow.GetComponent<RectXformMover>().MoveOn();
            int maxGoal = m_levelGoal.scoreGoals.Length - 1;
            UIManager.Instance.messageWindow.ShowScoreMessage(m_levelGoal.scoreGoals[maxGoal]);

            if (m_levelGoal.levelCounter == LevelCounter.Timer)
            {
                UIManager.Instance.messageWindow.ShowTimedGoal(m_levelGoal.timeLeft);
            }
            else
            {
                UIManager.Instance.messageWindow.ShowMovesGoal(m_levelGoal.movesLeft);
            }

            if (m_levelGoalCollected != null)
            {
                UIManager.Instance.messageWindow.ShowCollectionGoal(true);

                GameObject goalLayout = UIManager.Instance.messageWindow.collectionGoalLayout;

                if (goalLayout != null)
                {
                    UIManager.Instance.SetupCollectionGoalLayout(m_levelGoalCollected.collectionGoals,
                        goalLayout,
                        80);
                }
            }
            else
            {
                UIManager.Instance.messageWindow.ShowCollectionGoal(false);
            }
        }
    }


    IEnumerator postRequest(string url, string json, bool initialCall = false)
    {
        var uwr = new UnityWebRequest(url, "POST");
        byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(json);
        uwr.uploadHandler = (UploadHandler)new UploadHandlerRaw(jsonToSend);
        uwr.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        uwr.SetRequestHeader("Content-Type", "application/json");

        //Send the request then wait here until it returns
        yield return uwr.SendWebRequest();

        if (uwr.isNetworkError)
        {
            Debug.Log("Error While Sending: " + uwr.error);
        }

        else
        {
            if (initialCall)
            {
                jsonFromInitialCall = uwr.downloadHandler.text;
            }

            //   Debug.Log("Received: " + uwr.downloadHandler.text);
        }
    }

    // Record how the user rated the level
    public void rateLevel(int rating)
    {
        LevelStatsToCloudFunction(m_isWinner,
            m_levelGoal.movesLeft,
            ScoreManager.Instance.CurrentScore, levelKey, timePlaying, rating);
    }

    //  Skip the level
    public void SkipWin()
    {
        m_isWinner = true;
        m_isGameOver = true;
    }

    public void SkipLose()
    {
        m_isWinner = false;
        m_isGameOver = true;
    }
    
    public void setLevelServingStrategy(String lsstr)
    {
        levelsServingStrategy = lsstr;
        PlayerPrefs.SetString(levelsServingStrategyKey, levelsServingStrategy);
        PlayerPrefs.Save();
        Debug.Log("Serving strategy changed to " + levelsServingStrategy);
    }

    public void logScreen(string screenName)
    {
        logToBq(screenName);
        FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventScreenView,
            new Parameter(
                "Screen", screenName),
            getUserIdParam(),
            getlevelServingStrategy(),
            getGameVersion()
        );
    }

    public static Parameter getUserIdParam()
    {
        return new Parameter("userId", userId);
    }

    public static Parameter getlevelServingStrategy()
    {
        return new Parameter("levelServingStrategy", levelsServingStrategy);
    }

    public static Parameter getGameVersion()
    {
        return new Parameter("gameVersion", bundleVersion);
    }

    public string AssignWorldServed()
    {
        float randomNumber = UnityEngine.Random.value;

        if (randomNumber <= 0.5f)
        {
            return "gpt";
        }
        else
        {
            return "random";
        }
    }

    private void logToBq(string screenName)
    {
        var dictDataForAnalytics = new
        {
            createdDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            userId,
            currentLevel,
            levelGuid,
            screenName,
            worldServed = levelsServingStrategy,
            gameVersion = bundleVersion
        };

        string dataForAnalytics = JsonConvert.SerializeObject(dictDataForAnalytics);
        StartCoroutine(postRequest(analyticsUrl, dataForAnalytics));
    }

    private static int getBundleVersion()
    {
#if UNITY_EDITOR
        return Convert.ToInt32(PlayerSettings.bundleVersion);
#else
        return Convert.ToInt32(Application.version);
#endif
    }
}