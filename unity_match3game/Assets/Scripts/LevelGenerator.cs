using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;

public class LevelGenerator
{

    public static Level Generate(LevelInputsJson levelInputs)
    {
        Debug.Log(levelInputs);
        
        Level level = ScriptableObject.CreateInstance<Level>();
        System.Random random = new System.Random();
        
        // Set GUID to the level so it can later be sent to the cloud function
        GameManager.levelGuid = levelInputs.LevelGuid;

        
        // Board setup
        level.levelName = "Level " + GameManager.currentLevel.ToString();
        level.width = levelInputs.Width;
        level.height = levelInputs.Height;
        level.timeLeft = levelInputs.TimeSeconds;

        PieceType[] randomPrefabs = Piece.LoadRandomPieces(levelInputs.NumDifferentPieces);
        GameObject goalParent = new GameObject("GamePiece");
        int[] piecesToCollect = levelInputs.CollectionGoals;
        List<CollectionGoal> collectionGoals = new List<CollectionGoal>();
        
        

        for (int i = 0; i < piecesToCollect.Length; i++)
        {
            // Create a collection goal for each piece to collect, using a random prefab and the current piece to collect
            // Add this collection goal to the list of collection goals
            collectionGoals.Add(CollectionGoal.CreateCollectionGoal(goalParent, randomPrefabs[i], piecesToCollect[i]));
        }

        level.collectionGoals = collectionGoals.ToArray();
        
        level.gamePiecePrefabs = Piece.ConvertToGameObjects(randomPrefabs);
        level.scoreGoals = GenerateFractionArray(levelInputs.ScoreGoal);
        level.movesLeft = levelInputs.NumMoves;

        level.startingTiles = new StartingObject[0];
        level.startingGamePieces = new StartingObject[0];
        level.startingBlockers = new StartingObject[0];
        // Items below are currently unused
        /* 
        level.startingTiles = levelInputs.StartingTiles;
        level.startingGamePieces = levelInputs.StartingGamePieces;
        level.startingBlockers = levelInputs.StartingBlockers;
        level.chanceForCollectible = levelInputs.ChanceForCollectible;

        // Convert color strings into GameObject instances
        level.gamePiecePrefabs = Piece.LoadMultiplePrefabs(
            levelInputs.GamePiecePrefabs.Select(color => ConvertColorToPieceType(color)).ToArray()
        );

        // Create a game piece
        GameObject goalParent = new GameObject("GamePiece");

        level.collectionGoals = levelInputs.CollectionGoals.Select(goal =>
            CollectionGoal.CreateCollectionGoal(goalParent, ConvertColorToPieceType(goal.Color),
                int.Parse((string)goal.Num))).ToArray();

        // Goals setup
        level.scoreGoals = levelInputs.ScoreGoals;
        level.movesLeft = levelInputs.MovesLeft;
        level.timeLeft = levelInputs.TimeLeft;
        level.levelCounter = levelInputs.LevelCounter;
*/
        return level;
    }

    public static PieceType ConvertColorToPieceType(string color)
    {
        switch (color.ToLower())
        {
            case "blue":
                return PieceType.RoundBlue;
            case "bluebreakable":
//                return PieceType.RoundBlueBreakable;
//            case "green":
                return PieceType.RoundGreen;
            case "orange":
                return PieceType.RoundOrange;
            case "purple":
                return PieceType.RoundPurple;
            case "red":
                return PieceType.RoundRed;
            case "teal":
                return PieceType.RoundTeal;
            case "yellow":
                return PieceType.RoundYellow;
            default:
                throw new ArgumentException("Invalid color:" + color);
        }
    }
        
    public static int[] GenerateFractionArray(int num)
    {
        int[] fractions = new int[3]
        {
            num / 3,
            num * 2 / 3,
            num
        };

        return fractions;
    }
    
   
}



public class LevelInputsJson
{
    [JsonProperty("level_uid")]
    public string LevelGuid { get; set; }

    [JsonProperty("num_different_pieces")]
    public int NumDifferentPieces { get; set; }

    [JsonProperty("score_goal")]
    public int ScoreGoal { get; set; }

    [JsonProperty("board_width")]
    public int Width { get; set; }

    [JsonProperty("board_height")]
    public int Height { get; set; }

    [JsonProperty("num_moves")]
    public int NumMoves { get; set; }

    [JsonProperty("time_seconds")]
    public int TimeSeconds { get; set; }

    [JsonProperty("created_time")]
    public long CreatedTime { get; set; }
    
    [JsonProperty("collection_goals")]
    public int[] CollectionGoals { get; set; }
}
/*
public class LevelInputsJson
{
    public int LevelIndex { get; set; }
    public string LevelGuid { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string[] GamePiecePrefabs { get; set; }
    public StartingObject[] StartingTiles { get; set; }
    public StartingObject[] StartingGamePieces { get; set; }
    public StartingObject[] StartingBlockers { get; set; }
    public int ChanceForCollectible { get; set; }
    public CollectionGoalJson[] CollectionGoals { get; set; }
    public int[] ScoreGoals { get; set; }
    public int MovesLeft { get; set; }
    public int TimeLeft { get; set; }
    public LevelCounter LevelCounter { get; set; }
    
}

*/
public class CollectionGoalJson
{
    public string Color { get; set; }
    public string Num { get; set; }
}




