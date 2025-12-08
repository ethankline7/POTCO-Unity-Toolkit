using UnityEngine;
using UnityEditor;
using System.Linq;
using POTCO.Effects;

namespace POTCO.Editor.Effects
{
    public class EffectPreviewWindow : EditorWindow
    {
        [MenuItem("POTCO/Effect Previewer")]
        public static void ShowWindow()
        {
            GetWindow<EffectPreviewWindow>("Effect Previewer");
        }

        private int selectedIndex = 0;
        private string[] effectTypes = new string[] { 
            "Fire", "Explosion", "Wind", "Attune", "AttuneSmoke", 
            "Beam", "BlackhandCurse", "BlackSmoke",
            "Blast", "BlockShield", "BlueFlame", "Bonfire",
            "BossAura", "BossEffect", "BrazierFire", "BulletEffect", "BurpEffect",
            "CameraShaker", "CandleFlame", "CannonBlastSmoke", "CannonExplosion", "CannonMuzzleFire",
            "CannonSmokeSimple", "CannonSplash", "CausticsProjector", "CaveEffects", "CeilingDebris",
            "Chrysanthemum", "CleanseBlast", "CleanseRays", "CloudScud", "CombatEffect",
            "ConeRays", "CraterSmoke", "CurseHit", "DaggerProjectile", "DarkAura",
            "DarkMaelstrom", "DarkPortal", "DarkShipFog", "DarkStar", "DarkSteam",
            "DarkWaterFog", "DefenseCannonball", "DesolationChargeSmoke", "DesolationSmoke", "DirtClod",
            "DomeExplosion", "DrainLife", "Drown", "DustCloud", "DustRing",
            "DustRingBanish", "EnergySpiral", "EruptionSmoke"
        };
        
        private GameObject currentPreview;

        // --- Parameters ---
        // Fire
        private float fireCardScale = 64.0f;
        private int firePoolSize = 96;
        private float fireDuration = 10.0f;

        // Explosion
        private float explCardScale = 128.0f;
        private float explRadius = 8.0f;
        private float explDuration = 2.0f;

        // Wind
        private float windFadeTime = 0.7f;
        private Vector3 windTargetScale = new Vector3(2.0f, 2.0f, 2.0f);
        private Color windFadeColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
        private float windScrollSpeed = 3.0f;

        private void OnGUI()
        {
            GUILayout.Label("POTCO Effect Previewer", EditorStyles.boldLabel);
            
            selectedIndex = EditorGUILayout.Popup("Effect Type", selectedIndex, effectTypes);
            
            GUILayout.Space(10);
            GUILayout.Label("Parameters", EditorStyles.boldLabel);

            switch (effectTypes[selectedIndex])
            {
                case "Fire":
                    fireCardScale = EditorGUILayout.FloatField("Card Scale", fireCardScale);
                    firePoolSize = EditorGUILayout.IntField("Pool Size", firePoolSize);
                    fireDuration = EditorGUILayout.FloatField("Duration", fireDuration);
                    break;
                case "Explosion":
                    explCardScale = EditorGUILayout.FloatField("Card Scale", explCardScale);
                    explRadius = EditorGUILayout.FloatField("Radius", explRadius);
                    explDuration = EditorGUILayout.FloatField("Duration", explDuration);
                    break;
                case "Wind":
                    windFadeTime = EditorGUILayout.FloatField("Fade Time", windFadeTime);
                    windTargetScale = EditorGUILayout.Vector3Field("Target Scale", windTargetScale);
                    windFadeColor = EditorGUILayout.ColorField("Fade Color", windFadeColor);
                    windScrollSpeed = EditorGUILayout.FloatField("Scroll Speed", windScrollSpeed);
                    break;
                case "Attune":
                case "AttuneSmoke":
                    GUILayout.Label("No exposed parameters yet.");
                    break;
            }

            GUILayout.Space(20);

            if (GUILayout.Button(currentPreview == null ? "Spawn Effect" : "Respawn Effect"))
            {
                SpawnEffect();
            }
            
            if (GUILayout.Button("Destroy Preview"))
            {
                if (currentPreview != null) DestroyImmediate(currentPreview);
            }
        }

        private void SpawnEffect()
        {
            if (currentPreview != null) DestroyImmediate(currentPreview);
            
            GameObject go = new GameObject("EffectPreview");
            currentPreview = go;
            
            switch (effectTypes[selectedIndex])
            {
                case "Fire":
                    var fire = go.AddComponent<FireEffect>();
                    fire.cardScale = fireCardScale;
                    fire.poolSize = firePoolSize;
                    fire.duration = fireDuration;
                    break;
                case "Explosion":
                    var expl = go.AddComponent<ExplosionEffect>();
                    expl.cardScale = explCardScale;
                    expl.radius = explRadius;
                    expl.duration = explDuration;
                    break;
                case "Wind":
                    var wind = go.AddComponent<WindEffect>();
                    wind.fadeTime = windFadeTime;
                    wind.targetScale = windTargetScale;
                    wind.fadeColor = windFadeColor;
                    wind.scrollSpeed = windScrollSpeed;
                    break;
                case "Attune":
                    go.AddComponent<AttuneEffect>();
                    break;
                case "AttuneSmoke":
                    go.AddComponent<AttuneSmokeEffect>();
                    break;
                case "Beam":
                    go.AddComponent<BeamEffect>();
                    break;
                case "BlackhandCurse":
                    go.AddComponent<BlackhandCurseEffect>();
                    break;
                case "BlackSmoke":
                    go.AddComponent<BlackSmokeEffect>();
                    break;
                case "Blast":
                    go.AddComponent<BlastEffect>();
                    break;
                case "BlockShield":
                    go.AddComponent<BlockShieldEffect>();
                    break;
                case "BlueFlame":
                    go.AddComponent<BlueFlameEffect>();
                    break;
                case "Bonfire":
                    go.AddComponent<BonfireEffect>();
                    break;
                case "BossAura":
                    go.AddComponent<BossAuraEffect>();
                    break;
                case "BossEffect":
                    go.AddComponent<BossEffect>();
                    break;
                case "BrazierFire":
                    go.AddComponent<BrazierFireEffect>();
                    break;
                case "BulletEffect":
                    go.AddComponent<BulletEffect>();
                    break;
                case "BurpEffect":
                    go.AddComponent<BurpEffect>();
                    break;
                case "CameraShaker":
                    go.AddComponent<CameraShakerEffect>();
                    break;
                case "CandleFlame":
                    go.AddComponent<CandleFlameEffect>();
                    break;
                case "CannonBlastSmoke":
                    go.AddComponent<CannonBlastSmokeEffect>();
                    break;
                case "CannonExplosion":
                    go.AddComponent<CannonExplosionEffect>();
                    break;
                case "CannonMuzzleFire":
                    go.AddComponent<CannonMuzzleFireEffect>();
                    break;
                case "CannonSmokeSimple":
                    go.AddComponent<CannonSmokeSimpleEffect>();
                    break;
                case "CannonSplash":
                    go.AddComponent<CannonSplashEffect>();
                    break;
                case "CausticsProjector":
                    go.AddComponent<CausticsProjectorEffect>();
                    break;
                case "CaveEffects":
                    go.AddComponent<CaveEffects>();
                    break;
                case "Chrysanthemum":
                    go.AddComponent<ChrysanthemumEffect>();
                    break;
                case "CleanseBlast":
                    go.AddComponent<CleanseBlastEffect>();
                    break;
                case "CleanseRays":
                    go.AddComponent<CleanseRaysEffect>();
                    break;
                case "CloudScud":
                    go.AddComponent<CloudScudEffect>();
                    break;
                case "CombatEffect":
                    go.AddComponent<CombatEffect>();
                    break;
                case "ConeRays":
                    go.AddComponent<ConeRaysEffect>();
                    break;
                case "CraterSmoke":
                    go.AddComponent<CraterSmokeEffect>();
                    break;
                case "CurseHit":
                    go.AddComponent<CurseHitEffect>();
                    break;
                case "DaggerProjectile":
                    go.AddComponent<DaggerProjectileEffect>();
                    break;
                case "DarkAura":
                    go.AddComponent<DarkAuraEffect>();
                    break;
                case "DarkMaelstrom":
                    go.AddComponent<DarkMaelstromEffect>();
                    break;
                case "DarkPortal":
                    go.AddComponent<DarkPortalEffect>();
                    break;
                case "DarkShipFog":
                    go.AddComponent<DarkShipFogEffect>();
                    break;
                case "DarkStar":
                    go.AddComponent<DarkStarEffect>();
                    break;
                case "DarkWaterFog":
                    go.AddComponent<DarkWaterFogEffect>();
                    break;
                case "DefenseCannonball":
                    go.AddComponent<DefenseCannonballEffect>();
                    break;
                case "DesolationChargeSmoke":
                    go.AddComponent<DesolationChargeSmokeEffect>();
                    break;
                case "DesolationSmoke":
                    go.AddComponent<DesolationSmokeEffect>();
                    break;
                case "DomeExplosion":
                    go.AddComponent<DomeExplosionEffect>();
                    break;
                case "DrainLife":
                    go.AddComponent<DrainLifeEffect>();
                    break;
                case "Drown":
                    go.AddComponent<DrownEffect>();
                    break;
                case "DustCloud":
                    go.AddComponent<DustCloudEffect>();
                    break;
                case "DustRingBanish":
                    go.AddComponent<DustRingBanishEffect>();
                    break;
                case "EnergySpiral":
                    go.AddComponent<EnergySpiralEffect>();
                    break;
                case "EruptionSmoke":
                    go.AddComponent<EruptionSmokeEffect>();
                    break;
            }
            
            Selection.activeGameObject = go;
            SceneView.FrameLastActiveSceneView();
        }
    }
}
