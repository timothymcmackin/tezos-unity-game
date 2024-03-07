using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api;
using Api.Models;
using Beacon.Sdk.Beacon.Sign;
using Helpers;
using TezosSDK.Helpers.Coroutines;
using TezosSDK.Tezos;
using TezosSDK.Tezos.API.Models.Filters;
using UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using Type = Nft.NftType;

namespace Managers
{
    public class UserDataManager : MonoBehaviour
    {
        public static UserDataManager Instance;

        private string _connectedAddress;
        private string _pubKey;

        private List<Nft> _contractNfts;
        private List<Nft> _userNfts;
        private List<Nft> _equipment;
        private List<Reward> _rewards;

        public Action<List<Nft>> TokensReceived;
        public Action<List<Nft>> RewardsAndTokensLoaded;
        public Action<GameSession> GameStarted;

        [SerializeField] private int maxTokenCount = 20;
        [SerializeField] private string contract = "KT1DTJEAte2SE1dTJNWS1qSck8pCmGpVpD6X";
        [SerializeField] private string serverApiUrl = "https://static.turborouter.keenetic.pro/api";

        private GameApi _api;

        void Start()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            _equipment = new List<Nft>();
            _userNfts = new List<Nft>();
            _contractNfts = new List<Nft>();

            Instance = this;

            _api = new GameApi(serverApiUrl);

            TezosManager.Instance.Wallet.EventManager.WalletDisconnected += WalletDisconnected;
            TezosManager.Instance.Wallet.EventManager.WalletConnected += WalletConnected;
            TezosManager.Instance.Wallet.EventManager.PayloadSigned += PayloadSigned;
            TezosManager.Instance.Wallet.EventManager.ContractCallCompleted += OperationCompleted;

            DontDestroyOnLoad(gameObject);

            SceneManager.activeSceneChanged += ChangedActiveScene;
        }

        private void PayloadSigned(SignResult payload)
        {
            var routine = _api.VerifyPayload(_pubKey, payload.Signature, verified =>
            {
                if (!verified) return;

                PlayerPrefs.SetString("Address", _connectedAddress);
                GetMenuManager()?.EnableGameMenu();
            });
            CoroutineRunner.Instance.StartWrappedCoroutine(routine);
        }

        private void OperationCompleted(OperationResult operationResult)
        {
            GetMenuManager().ShowSuccessOperationHash(operationResult.TransactionHash);
        }

        private void WalletConnected(WalletInfo wallet)
        {
            _connectedAddress = wallet.Address;
            _pubKey = wallet.PublicKey;
            if (string.IsNullOrEmpty(PlayerPrefs.GetString("Address", null)))
            {
                var routine = _api.GetPayload(_pubKey,
                    payload =>
                    {
                        TezosManager.Instance.Wallet.RequestSignPayload(SignPayloadType.micheline, payload);
                    });
                CoroutineRunner.Instance.StartWrappedCoroutine(routine);
            }
            else
            {
                GetMenuManager()?.EnableGameMenu();
            }

            StartCoroutine(LoadGameNfts());
        }

        private void WalletDisconnected(WalletInfo wallet)
        {
            PlayerPrefs.SetString("Address", null);
            GetMenuManager()?.DisableGameMenu();
        }

        public void StartGame()
        {
            var routine = _api.CreateGameSession(_connectedAddress, session =>
                GameStarted?.Invoke(session));
            CoroutineRunner.Instance.StartWrappedCoroutine(routine);
        }

        public void EndGame(string gameId)
        {
            var routine = _api.EndGameSession(gameId);
            CoroutineRunner.Instance.StartWrappedCoroutine(routine);
        }

        public void PauseGame(string gameId)
        {
            var routine = _api.PauseGame(gameId);
            CoroutineRunner.Instance.StartWrappedCoroutine(routine);
        }
        
        public void ResumeGame(string gameId)
        {
            var routine = _api.ResumeGame(gameId);
            CoroutineRunner.Instance.StartWrappedCoroutine(routine);
        }

        public void KillBoss(
            string gameId,
            int boss)
        {
            var routine = _api.KillBoss(gameId, boss);
            CoroutineRunner.Instance.StartWrappedCoroutine(routine);
        }

        private IEnumerator LoadGameNfts()
        {
            if (string.IsNullOrEmpty(_connectedAddress)) yield break;

            var userTokensCoroutine = CoroutineRunner.Instance.StartCoroutine(
                TezosManager.Instance.Tezos.API.GetTokensForOwner(tbs =>
                    {
                        if (tbs == null) return;

                        var userTokens = tbs.ToList();
                        if (userTokens.Count > 0)
                        {
                            var tokens = userTokens
                                .Where(t => t.TokenContract.Address == contract)
                                .ToList();

                            var options = new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                            };
                            options.Converters.Add(new JsonStringEnumConverter());
                            options.Converters.Add(new NftConverter());

                            _userNfts.Clear();
                            
                            foreach (var t in tokens)
                            {
                                try
                                {
                                    var nft = t.TokenMetadata.Deserialize<Nft>(options);

                                    if (nft == null ||
                                        nft.Type == Type.None ||
                                        nft.GameParameters == null) continue;

                                    nft.TokenId = int.Parse(t.TokenId);
                                    _userNfts.Add(nft);
                                }
                                catch (Exception e)
                                {
                                    Debug.Log("Serialization error: " + e);
                                }
                            }

                            TokensReceived?.Invoke(_userNfts);
                        }
                        else
                        {
                            Debug.Log($"{_connectedAddress} has no tokens");
                        }
                    },
                    owner: _connectedAddress,
                    withMetadata: true,
                    maxItems: maxTokenCount,
                    orderBy: new TokensForOwnerOrder.Default(0)));

            var contractTokensCoroutine = CoroutineRunner.Instance.StartCoroutine(
                TezosManager.Instance.Tezos.API.GetTokensForContract(tokens =>
                    {
                        Debug.Log(tokens);
                        var options = new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        };
                        options.Converters.Add(new JsonStringEnumConverter());
                        options.Converters.Add(new NftConverter());
                        
                        _contractNfts.Clear();

                        foreach (var t in tokens)
                        {
                            try
                            {
                                var nft = t.TokenMetadata.Deserialize<Nft>(options);

                                if (nft == null ||
                                    nft.Type == Type.None ||
                                    nft.GameParameters == null) continue;

                                nft.TokenId = int.Parse(t.TokenId);
                                _contractNfts.Add(nft);
                                
                            }
                            catch (Exception e)
                            {
                                Debug.Log("Serialization error: " + e);
                            }
                        }
                    },
                    contractAddress: contract,
                    withMetadata: true,
                    maxItems: maxTokenCount,
                    orderBy: new TokensForContractOrder.Default(0)));

            var rewardsCoroutine = CoroutineRunner.Instance.StartCoroutine(
                _api.GetRewardsList(
                    _connectedAddress, 
                    rewards =>
                    {
                        var menuManager = GetMenuManager();
                        _rewards = new List<Reward>(rewards);
                        
                        menuManager.SetRewardsAmount(
                            _rewards.Aggregate(0, (acc, reward) => acc + reward.Amount)
                        );
                    }
                )
            );

            yield return userTokensCoroutine;
            yield return contractTokensCoroutine;
            yield return rewardsCoroutine;

            var rewardNfts = GetRewardNfts().ToList();
            RewardsAndTokensLoaded?.Invoke(rewardNfts);
        }

        public IEnumerable<Nft> GetRewardNfts()
        {
            var rewardTokenIds = _rewards
                .Select(r => r.TokenId)
                .ToArray();
                        
            var rewardNftList = _contractNfts
                .Where(nft => rewardTokenIds.Contains(nft.TokenId))
                .ToList();
                        
            foreach (var rewardNft in rewardNftList)
            {
                rewardNft.Amount = _rewards
                    .Find(r => r.TokenId == rewardNft.TokenId)
                    .Amount;
            }

            return rewardNftList;
        }

        // Called from JS side after captcha checked.
        public void ClaimReward(string captchaData)
        {
            var uiManager = GetMenuManager();
            uiManager.HideRewardsWindow();
            uiManager.ShowTokensAwaitingBadge();

            CoroutineRunner.Instance.StartCoroutine(
                _api.ClaimReward(
                    _connectedAddress,
                    captchaData,
                    claimRewardResponse =>
                    {
                        if (string.IsNullOrEmpty(claimRewardResponse.OperationHash)) return;
                        OperationCompleted(new OperationResult
                            {
                                TransactionHash = claimRewardResponse.OperationHash
                            }
                        );
                        StartCoroutine(LoadGameNfts());
                        uiManager.HideTokensAwaitingBadge();
                    }
                )
            );
        }

        private UiMenuManager GetMenuManager()
        {
            if (!Camera.main) return null;
            Camera.main.TryGetComponent<UiMenuManager>(out var manager);
            return manager;
        }

        private void ChangedActiveScene(Scene current, Scene next)
        {
            if (next.name == "Main")
            {
                _userNfts.Clear();
                _equipment.Clear();
                _contractNfts.Clear();
                StartCoroutine(LoadGameNfts());
                GetMenuManager()?.EnableGameMenu();
            }
        }

        public List<Nft> GetEquipment() => _equipment;

        public void SetEquipment(List<Nft> equipment)
        {
            _equipment = equipment;
        }

        private void OnDisable()
        {
            if (TezosManager.Instance == null) return;

            TezosManager.Instance.Wallet.EventManager.WalletDisconnected -= WalletDisconnected;
            TezosManager.Instance.Wallet.EventManager.WalletConnected -= WalletConnected;
            TezosManager.Instance.Wallet.EventManager.PayloadSigned -= PayloadSigned;
            TezosManager.Instance.Wallet.EventManager.ContractCallCompleted -= OperationCompleted;

            SceneManager.activeSceneChanged -= ChangedActiveScene;
        }
    }
}