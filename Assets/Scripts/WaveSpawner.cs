using UnityEngine;

// Spawns a new wave when the screen is clear. New bullet patterns unlock as waves go up; every 5th wave is a boss.
public class WaveSpawner : MonoBehaviour
{
    const float DelayBetweenWaves = 1.5f;

    public int Wave { get; private set; }
    float countdown;

    public void ResetWaves()
    {
        Wave = 0;
        countdown = 1f;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm.IsGameOver || Enemy.All.Count > 0) return;

        countdown -= Time.deltaTime;
        if (countdown > 0f) return;

        countdown = DelayBetweenWaves;
        SpawnWave(gm.Bounds);
    }

    void SpawnWave(Rect b)
    {
        Wave++;
        float difficulty = Wave;

        if (Wave % 5 == 0)
        {
            var bossPattern = Wave % 10 == 0 ? BulletPattern.Ring : BulletPattern.Spiral;
            Enemy.Spawn(new Vector2(0f, b.yMax + 2f), new Vector2(0f, b.yMax - 3f), bossPattern, 40 + Wave * 6, difficulty, boss: true);
            return;
        }

        // Wave 1: Fan, 2: +Aimed, 3: +Ring, 4+: +Spiral
        int unlocked = Mathf.Clamp(Wave, 1, 4);
        int count = Mathf.Min(2 + Wave / 2, 6);
        int health = 3 + Wave;

        for (int i = 0; i < count; i++)
        {
            float t = (i + 1f) / (count + 1f);
            float x = Mathf.Lerp(b.xMin + 0.8f, b.xMax - 0.8f, t);
            float y = b.yMax - Random.Range(1.5f, 4.5f);
            var pattern = (BulletPattern)Random.Range(0, unlocked);
            Enemy.Spawn(new Vector2(x, b.yMax + 1f + i * 0.4f), new Vector2(x, y), pattern, health, difficulty);
        }
    }
}
