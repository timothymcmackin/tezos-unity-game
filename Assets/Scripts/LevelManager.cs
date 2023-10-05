using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

public class LevelManager : MonoBehaviour
{
    public GameObject[] enemies;
    public Transform[] spawnPoints;

    [Header("Spawn values:")]
    public float startTimeBtwSpawn;
    public float decreaseTime;
    public float minTime;
    public float enemyHealth;
    public float increaseHealth;
    public float enemyDamage;
    public float increaseDamage;

    private int _score;
    public Action<int> scoreUpdated;
    public Action playerDied;

    private SoundManager _soundManager;
    private PlayerController _player;
    private float _timeBtwSpawn;

    // Start is called before the first frame update
    void Start()
    {
        _player = GameObject.FindGameObjectWithTag("Player")
            .GetComponent<PlayerController>();
        _soundManager = GetComponent<SoundManager>();
        _score = 0;
    }

    
    // Update is called once per frame
    void Update()
    {
        if (_player.GetPlayerHealth() <= 0)
        {
            _soundManager.Lose();
            Stop();
            playerDied.Invoke();
        }
        else
        {
            SpawnEnemy();
        }
    }

    private void SpawnEnemy()
    {
        enemyDamage += Time.deltaTime * increaseDamage;
        enemyHealth += Time.deltaTime * increaseHealth;

        if (_timeBtwSpawn <= 0)
        {
            var randomEnemy = Random.Range(0, enemies.Length);
            var randomPoint = Random.Range(0, spawnPoints.Length);
            var enemyScript = Instantiate(enemies[randomEnemy], spawnPoints[randomPoint].position, Quaternion.identity).GetComponent<Enemy>();
            enemyScript.health = enemyHealth;
            enemyScript.meleeDamage = enemyDamage;
            enemyScript.enemyKilled += EnemyKilled;
            _timeBtwSpawn = startTimeBtwSpawn;
            if (startTimeBtwSpawn > minTime)
            {
                startTimeBtwSpawn -= decreaseTime;
            }
        }
        else
        {
            _timeBtwSpawn -= Time.deltaTime;
        }
    }

    public int GetScore()
    {
        return _score;
    }

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        Time.timeScale = 1;
    }

    private void EnemyKilled()
    {
        _score++;
        scoreUpdated.Invoke(_score);
        _soundManager.Death();
    }

    private void Stop()
    {
        Time.timeScale = 0;

        _player.enabled = false;
        var enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var e in enemies)
        {
            e.GetComponent<Enemy>().enabled = false;
        }

        enabled = false;
    }
}