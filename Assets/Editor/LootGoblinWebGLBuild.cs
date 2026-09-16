using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Batch-mode entry point for producing the GitHub Pages WebGL artifact.
/// </summary>
public static class LootGoblinWebGLBuild
{
    private const string OutputDirectory = "Builds/WebGL";

    public static void Build()
    {
        string[] scenes = GetEnabledBuildScenes();
        string outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputDirectory));

        Directory.CreateDirectory(outputPath);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException(
                $"Loot Goblin WebGL build failed with result: {report.summary.result}.");
        }

        Debug.Log($"Loot Goblin WebGL build succeeded: {outputPath}");
    }

    private static string[] GetEnabledBuildScenes()
    {
        List<string> scenes = new List<string>();

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
            {
                scenes.Add(scene.path);
            }
        }

        if (scenes.Count == 0)
        {
            throw new BuildFailedException("No enabled scenes were found in Build Settings.");
        }

        return scenes.ToArray();
    }
}
