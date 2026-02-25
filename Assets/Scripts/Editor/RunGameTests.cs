using UnityEditor;
using UnityEngine;

public class RunGameTests
{
    [MenuItem("Rummy500/Run Core Tests")]
    public static void Run()
    {
        Debug.Log("=== Running Rummy500 Core Tests ===");
        GameTests.RunAll();
    }
}