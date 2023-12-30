using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public enum PieceType
{
    RoundBlue,
 //   RoundBlueBreakable,
    RoundGreen,
    RoundOrange,
    RoundPurple,
    RoundRed,
    RoundTeal,
    RoundYellow,
}

public class Piece
{
    public static Dictionary<PieceType, string> PrefabPaths = new Dictionary<PieceType, string>
    {
        { PieceType.RoundBlue, "RoundPieces/RoundBlue" },
 /*       { PieceType.RoundBlueBreakable, "RoundPieces/RoundBlueBreakable" },*/
        { PieceType.RoundGreen, "RoundPieces/RoundGreen" },
        { PieceType.RoundOrange, "RoundPieces/RoundOrange" },
        { PieceType.RoundPurple, "RoundPieces/RoundPurple" },
        { PieceType.RoundRed, "RoundPieces/RoundRed" },
        { PieceType.RoundTeal, "RoundPieces/RoundTeal" },
        { PieceType.RoundYellow, "RoundPieces/RoundYellow" },
    };

    public static GameObject LoadPrefab(PieceType type)
    {
        if (PrefabPaths.TryGetValue(type, out var path))
        {
            return Resources.Load<GameObject>(path);
        }

        return null;
    }

    public static GameObject[] LoadMultiplePrefabs(params PieceType[] types)
    {
        GameObject[] prefabs = new GameObject[types.Length];

        for (int i = 0; i < types.Length; i++)
        {
            prefabs[i] = LoadPrefab(types[i]);
        }

        return prefabs;
    }
    public static GameObject[] LoadRandomPrefabs(int numPieces)
    {
        GameObject[] prefabs = new GameObject[numPieces];
        List<PieceType> pieceTypes = Enum.GetValues(typeof(PieceType)).Cast<PieceType>().ToList();

        for (int i = 0; i < numPieces; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, pieceTypes.Count);
            PieceType randomPieceType = pieceTypes[randomIndex];
            prefabs[i] = LoadPrefab(randomPieceType);
            pieceTypes.RemoveAt(randomIndex);
        }

        return prefabs;
    }
    
    public static PieceType[] LoadRandomPieces(int numPieces)
    {
        List<PieceType> result = new List<PieceType>();
        List<PieceType> pieceTypes = Enum.GetValues(typeof(PieceType)).Cast<PieceType>().ToList();

        for (int i = 0; i < numPieces; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, pieceTypes.Count);
            PieceType randomPieceType = pieceTypes[randomIndex];
            result.Add(randomPieceType);
            pieceTypes.RemoveAt(randomIndex);
        }

        return result.ToArray();
    }
    
    public static GameObject[] ConvertToGameObjects(PieceType[] pieceTypes)
    {
        GameObject[] gameObjects = new GameObject[pieceTypes.Length];

        for (int i = 0; i < pieceTypes.Length; i++)
        {
            gameObjects[i] = LoadPrefab(pieceTypes[i]);
        }

        return gameObjects;
    }


}