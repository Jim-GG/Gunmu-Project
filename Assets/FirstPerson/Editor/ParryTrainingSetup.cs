using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FirstPerson.Editor
{
    public static class ParryTrainingSetup
    {
        [MenuItem("Tools/Gauntlet/Configure Parry Training")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first");
            var player = GameObject.Find("First Person Player");
            if (!player) throw new InvalidOperationException("Run Enable First Person Gameplay first");
            Configure(player);
            var position = player.transform.position; var rotation = player.transform.rotation;
            try
            {
                player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                PrefabUtility.SaveAsPrefabAsset(player, "Assets/FirstPerson/Prefabs/FirstPersonPlayer.prefab");
            }
            finally { player.transform.SetPositionAndRotation(position, rotation); }
            EditorSceneManager.MarkSceneDirty(player.scene);
            EditorSceneManager.SaveScene(player.scene);
            AssetDatabase.SaveAssets();
        }
        public static void Configure(GameObject player)
        {
            var combat = player.GetComponent<GauntletCombat>();
            var vitals = player.GetComponent<FirstPersonVitals>();
            if (!vitals) vitals = Undo.AddComponent<FirstPersonVitals>(player);
            var enemyGo = GameObject.Find("Parry Training Enemy");
            if (!enemyGo)
            {
                enemyGo = new GameObject("Parry Training Enemy");
                Undo.RegisterCreatedObjectUndo(enemyGo, "Create parry training enemy");
                enemyGo.transform.position = player.transform.position + player.transform.forward * 2.4f;
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule); body.name = "Body";
                body.transform.SetParent(enemyGo.transform, false); body.transform.localPosition = Vector3.up * .9f;
                body.transform.localScale = new Vector3(.65f,.9f,.65f);
                var arm = GameObject.CreatePrimitive(PrimitiveType.Cube); arm.name = "Striking Arm";
                arm.transform.SetParent(enemyGo.transform, false); arm.transform.localPosition = new Vector3(.45f,1.25f,.3f);
                arm.transform.localScale = new Vector3(.2f,.2f,.85f);
                UnityEngine.Object.DestroyImmediate(arm.GetComponent<Collider>());
            }
            var enemy = enemyGo.GetComponent<ParryTrainingEnemy>();
            if (!enemy) enemy = Undo.AddComponent<ParryTrainingEnemy>(enemyGo);
            Undo.RecordObject(enemy, "Configure training attack");
            enemy.target = combat; enemy.vitals = vitals;
            enemy.body = enemyGo.transform.Find("Body").GetComponent<Renderer>();
            enemy.strikingArm = enemyGo.transform.Find("Striking Arm");
            if (!AssetDatabase.IsValidFolder("Assets/FirstPerson/Materials")) AssetDatabase.CreateFolder("Assets/FirstPerson", "Materials");
            const string path = "Assets/FirstPerson/Materials/TrainingEnemy.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", new Color(.5f,.2f,.08f));
                AssetDatabase.CreateAsset(material, path);
            }
            foreach (var renderer in enemyGo.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = material;
            var hudGo = GameObject.Find("Gauntlet Training HUD");
            if (!hudGo) { hudGo = new GameObject("Gauntlet Training HUD"); Undo.RegisterCreatedObjectUndo(hudGo, "Create training HUD"); }
            var hud = hudGo.GetComponent<GauntletTrainingHUD>();
            if (!hud) hud = Undo.AddComponent<GauntletTrainingHUD>(hudGo);
            Undo.RecordObject(hud, "Configure training HUD");
            hud.combat = combat; hud.vitals = vitals; hud.enemy = enemy;
        }
    }
}
