using UnityEngine;
using System.Collections.Generic;

public class EggVertex
{
    public Vector3 position;
    public Vector3 normal;
    public Vector2 uv; // Primary UV set
    public Dictionary<string, Vector2> namedUVs = new Dictionary<string, Vector2>(); // Named UV sets
    public Color color = Color.white;
    public Dictionary<string, float> boneWeights = new Dictionary<string, float>();
    public string vertexPoolName = ""; // Track which vertex pool this vertex belongs to
}