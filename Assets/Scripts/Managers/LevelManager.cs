using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Api.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace Managers
{
    /// !!! WARNING !!!
    /// <summary>
    /// Each enemy prefab must have a unique threat value
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public GameObject[] enemies;
        public GameObject[] bosses;
        public Transform[] spawnPoints;
        public int minSpawnDistanceToPlayer;

        [Header("LOOT:")] 
        [SerializeField] private GameObject[] supplyItems;
        [SerializeField] private int dropChance;
        [SerializeField] private GameObject[] weapons;
        [SerializeField] private GameObject nftItem;

        [Header("WAVES PARAMS:")] 
        [SerializeField] private float waveRateInSec;
        [SerializeField] private int waveThreatEnhancement;
        [SerializeField] private int minThreatInPercent;
        [SerializeField] private int bossRateMod;

        private int _wave;
        private int _waveThreat;
        private int _currentThreat;

        /// <summary>
        /// Store enemies by threat
        /// </summary>
        /// <typeparam name="int">Enemy threat</typeparam>
        /// <typeparam name="GameObject">Enemy prefab</typeparam>
        private Dictionary<int, GameObject> _enemiesWithThreat;

        public Action<int, int> GameScoreUpdated;
        public Action<int, int> NewWaveHasBegun;
        public Action<Enemy, int, int> BossSpawned;
        public Action<Enemy> BossKilled;
        public Action DropNft;
        public Action PlayerDied;
        public Action PauseGame;
        public Action<bool> ResumeGame;
        public Action ResumeGameRequest;
        [HideInInspector] public bool gameIsPaused;

        private SoundManager _soundManager;
        private PlayerController _player;
        private float _timeBtwSpawn;
        private float _distanceBtwItemDrop = 2f;

        private GameSession _gameSession;
        private GameResult _gameResult;

        // Start is called before the first frame update
        void Start()
        {
            UserDataManager.Instance.GameStarted += GameStarted;
            _gameSession = UserDataManager.Instance.GetCurrentGameSession();
            _player = GameObject.FindGameObjectWithTag("Player")
                .GetComponent<PlayerController>();
            _player.HealthChanged += PlayerHealthChanged;
            _soundManager = GetComponent<SoundManager>();
            _gameResult = gameObject.AddComponent<GameResult>();
            _gameResult.Init(_gameSession?.GameId);
            InitEnemies();
        }

        private void GameStarted()
        {
            _gameSession = UserDataManager.Instance.GetCurrentGameSession();
            SceneManager.LoadScene("Game");
            Time.timeScale = 1;
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            
            gameIsPaused = !gameIsPaused;
            Pause();
        }

        private void FixedUpdate()
        {
            if (gameIsPaused || _gameSession == null) return;

            CheckWave();
        }

        private void InitEnemies()
        {
            _enemiesWithThreat = new Dictionary<int, GameObject>();

            for (var i = 0; i < enemies.Length; i++)
            {
                _enemiesWithThreat.Add(
                    enemies[i].GetComponent<Enemy>().threat,
                    enemies[i]);
            }
        }

        private GameObject GetEnemyByThreat(int threatValue)
        {
            _enemiesWithThreat.TryGetValue(threatValue, out var value);
            return value;
        }

        private void CheckWave()
        {
            if (_timeBtwSpawn <= 0 ||
                _currentThreat <= _waveThreat * minThreatInPercent / 100)
            {
                _timeBtwSpawn = waveRateInSec;
                _wave++;
                _waveThreat += waveThreatEnhancement;
                _currentThreat += _waveThreat;

                if (_wave % bossRateMod == 0)
                {
                    SpawnBoss();
                }
                else
                {
                    SpawnEnemies();
                }
                
                GameScoreUpdated?.Invoke(_gameResult.GetScore(), _currentThreat);
            }
            else
            {
                _timeBtwSpawn -= Time.deltaTime;
            }
        }

        public void Restart()
        {
            UserDataManager.Instance.StartGame();
        }

        private void SpawnBoss()
        {
            var rnd = Random.Range(0, bosses.Length);
            var spawnPoint = GetRandomSpawnPoint();

            var rndBoss = bosses[rnd];
            
            var boss = Instantiate(
                rndBoss,
                new Vector3(
                    spawnPoint.x,
                    spawnPoint.y,
                    spawnPoint.z),
                Quaternion.identity);

            boss.name = rndBoss.name;
            
            var bossScript = boss.GetComponent<Enemy>();

            if (_gameSession != null)
            {
                var bossIndex = _wave / bossRateMod;
                bossScript.AppointBoss(bossIndex);
                
                if (weapons.Length >= bossIndex)
                    bossScript.AddKillAward(weapons[bossIndex-1]);
                var drop = _gameSession
                    .GameDrop
                    .FirstOrDefault(gd => gd.Boss == bossIndex);
                if (drop != null)
                    bossScript.AddKillAward(nftItem);
            }

            bossScript.threat = _waveThreat;
            bossScript.EnemyKilled += EnemyKilled;

            _soundManager.Roar();
            BossSpawned?.Invoke(bossScript, _wave, _waveThreat);
        }

        private void SpawnEnemies()
        {
            var totalWaveThreat = 0;

            while (totalWaveThreat < _waveThreat)
            {
                var rnd = Random.Range(0, _enemiesWithThreat.Count);
                var enemyKeyValuePair = _enemiesWithThreat.ElementAt(rnd);
                var spawnPoint = GetRandomSpawnPoint();

                if (totalWaveThreat + enemyKeyValuePair.Key > _waveThreat)
                {
                    var lastEnemy = GetEnemyByThreat(_waveThreat - totalWaveThreat);
                    if (lastEnemy != null)
                    {
                        lastEnemy = Instantiate(
                            lastEnemy,
                            spawnPoint,
                            Quaternion.identity);
                    }
                    else
                    {
                        enemyKeyValuePair = _enemiesWithThreat
                            .Aggregate((l, r) =>
                                l.Key < r.Key ? l : r);

                        lastEnemy = Instantiate(
                            enemyKeyValuePair.Value,
                            spawnPoint,
                            Quaternion.identity);
                    }

                    SubscribeToKillEvents(lastEnemy);

                    totalWaveThreat += lastEnemy.GetComponent<Enemy>().threat;
                }
                else
                {
                    var enemy = Instantiate(
                        enemyKeyValuePair.Value,
                        spawnPoint,
                        Quaternion.identity);

                    SubscribeToKillEvents(enemy);

                    totalWaveThreat += enemyKeyValuePair.Key;
                }
            }

            NewWaveHasBegun?.Invoke(_wave, _waveThreat);
        }

        private Vector3 GetRandomSpawnPoint()
        {
            var randomPoint = Random.Range(0, spawnPoints.Length);
            while (Vector3.Distance(
                       _player.transform.position,
                       spawnPoints[randomPoint].position) 
                   < minSpawnDistanceToPlayer)
            {
                randomPoint = Random.Range(0, spawnPoints.Length);
            }

            return spawnPoints[randomPoint].position;
        }

        public void EnemyKilled(Enemy enemy, Transform killPosition, List<GameObject> killAwards)
        {
            _gameResult.UpdateScore();
            _currentThreat -= enemy.threat;
            GameScoreUpdated?.Invoke(_gameResult.GetScore(), _currentThreat);

            _soundManager.Death();

            for (var i = 0; i < killAwards.Count; i++)
            {
                var position = killPosition.position;
                
                position = new Vector3(
                    position.x + _distanceBtwItemDrop * i,
                    0f, 
                    position.z + _distanceBtwItemDrop * i);

                var award = Instantiate(killAwards[i], position, Quaternion.identity);
                award.name = killAwards[i].name;
            }
            
            if (!enemy.IsTheBoss()) return;
            
            BossKilled?.Invoke(enemy);
            
            if (_gameSession == null) return;
            
            var drop = _gameSession
                .GameDrop
                .FirstOrDefault(gd => gd.Boss == enemy.GetBossIndex());
            if (drop != null)
            {
                DropNft?.Invoke();
                _soundManager.Drop();
            }

            UserDataManager.Instance.KillBoss(
                _gameSession?.GameId,
                enemy.GetBossIndex());
        }

        private void SubscribeToKillEvents(GameObject enemy)
        {
            var enemyScript = enemy.GetComponent<Enemy>();

            foreach (var loot in supplyItems)
            {
                var randomNumber = Random.Range(1, 100);
                if (randomNumber <= dropChance)
                {
                    enemyScript.AddKillAward(loot);
                    break;
                }
            }

            enemyScript.EnemyKilled += EnemyKilled;
        }
        
        private void PlayerHealthChanged(float _, float health, bool __)
        {
            if (health > 0) return;
            EndGame();
        }

        private void Pause()
        {
            if(gameIsPaused)
            {
                UserDataManager.Instance.PauseGame(_gameSession?.GameId);
                PauseGame?.Invoke();
                Time.timeScale = 0f;
            }
            else 
            {
                ResumeGameRequest();
            }
        }

        public void ResumeRequest()
        {
            ResumeGameRequest?.Invoke();
        }

        public void Resume()
        {
            UserDataManager.Instance.ResumeGame(_gameSession?.GameId, resumed =>
            {
                if (resumed)
                {
                    Time.timeScale = 1;
                    gameIsPaused = false;
                }
                
                ResumeGame?.Invoke(resumed);
            });
        }

        public void QuitGame()
        {
            UserDataManager.Instance.EndGame(_gameResult);
            LoadScene("Main");
        }

        private void EndGame()
        {
            UserDataManager.Instance.EndGame(_gameResult);
            _soundManager.Lose();
            PlayerDied?.Invoke();
            Time.timeScale = 0f;
        }

        public GameResult GetGameResult() => _gameResult;

        public void LoadScene(string scene)
        {
            if (scene != "")
            {
                StartCoroutine(LoadAsynchronously(scene));
            }
        }
        
        private IEnumerator LoadAsynchronously(string scene)
        {
            if (scene == "") yield break;
            
            var asyncLoad = SceneManager.LoadSceneAsync(scene);
            
            while (!asyncLoad.isDone)
            {
                yield return null;
            }
        }

        protected void OnDisable()
        {
            _player.HealthChanged -= PlayerHealthChanged;
            UserDataManager.Instance.GameStarted -= GameStarted;
        }
    }
}