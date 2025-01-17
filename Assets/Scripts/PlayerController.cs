using System;
using System.Collections.Generic;
using System.Linq;
using Animations;
using Helpers;
using Managers;
using UnityEngine;
using Weapons;
using Type = Nft.NftType;
using LootScript = Loot.Loot;
using WeaponType = Weapons.Weapon.WeaponType;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float health;
    [SerializeField] private float moveSpeed;
    [SerializeField] private float sprintSpeed;
    [SerializeField] private float sprintCooldown;
    [SerializeField] private float sprintDuration;

    /// <summary>
    /// Represent equipped weapons
    /// </summary>
    /// <typeparam name="string">Key by weapon type</typeparam>
    /// <typeparam name="GameObject">Equipped weapon</typeparam>
    private Dictionary<string, GameObject> _equippedWeapons;
    
    [SerializeField] private GameObject[] allWeapons;
    [SerializeField] private GameObject shield;
    [SerializeField] protected LayerMask lookAtMask;

    private float _maxHealth;
    private Weapon _currentWeapon;
    private WeaponType _weaponType;
    private Shield _shieldScript;
    private Vector3 _movement;
    private Vector3 _moveVector;
    private Rigidbody _rb;
    private Ray _ray;
    private RaycastHit _hit;
    private float _normalSpeed;
    private float _sprintTime;
    private float _timeBtwSprints;
    private bool _isSprinting;
    private bool _canSprint;
    private Animator _animator;

    private List<Nft> _userNfts;
    
    private float _damageIncreaseInPercent;
    private float _damageReflectionInPercent;

    public Action<float, float, bool> HealthChanged;
    public Action<Weapon> WeaponSwitched;
    public Action<float, float> SprintCooldownContinues;
    public Action SprintCooldownStarted;
    public Action SprintCooldownEnded;
    public Action PlayerInitialized;

    private LevelManager _levelManager;
    private SoundManager _soundManager;

    private void Awake()
    {
        var gameController = GameObject.FindGameObjectWithTag("GameController"); 
        _levelManager = gameController.GetComponent<LevelManager>();
        _soundManager = gameController.GetComponent<SoundManager>();
        _shieldScript = shield.GetComponent<Shield>();
        EquipPlayer();
    }

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        _weaponType = _currentWeapon.weaponType;
        WeaponSwitched?.Invoke(_currentWeapon);
        _normalSpeed = moveSpeed;
        _timeBtwSprints = sprintCooldown;
        _maxHealth = health;
        
        PlayerInitialized?.Invoke();
    }

    private void EquipPlayer()
    {
        _equippedWeapons = new Dictionary<string, GameObject>();
        var baseWeapons = GetAllWeapons()
            .Where(w => w.GetComponent<Weapon>().baseWeapon);

        foreach (var weapon in baseWeapons)
        {
            var weaponType = weapon.GetComponent<Weapon>().weaponType.ToString();
            if (_equippedWeapons.ContainsKey(weaponType))
                return;

            _equippedWeapons.Add(weaponType, weapon);
        }

        var equipment = UserDataManager.Instance.GetEquipment();
        
        foreach (var item in equipment)
        {
            if (item.Type is Type.Gun or Type.Shotgun or Type.Smg or Type.Explosive)
            {
                AddWeapon(item);   
            }
            else
            {
                UpdatePlayerSkills(item);
            }
        }
        
        EnableDefaultGun();
    }

    private void EnableDefaultGun()
    {
        var gunType = WeaponType.Gun.ToString();
        foreach (var weapon in _equippedWeapons
                     .Where(w => w.Key == gunType))
        {
            weapon.Value.SetActive(true);
            var weaponScript = weapon.Value.GetComponent<Weapon>();
            weaponScript.isUnlocked = false;
            _currentWeapon = weaponScript;
            _weaponType = _currentWeapon.weaponType;
            break;
        }   
    }

    private void UpdatePlayerSkills(Nft module)
    {
        if (module.Type is not (Type.Module or Type.Ability or Type.Armor) 
            || module.GameParameters == null) return;

        if (module.GameParameters.Exists(p => p.Name == "Health"))
        {
            var healthParam = module.GameParameters
                .First(p => p.Name == "Health");
            _maxHealth = healthParam.MeasureType == GameParameters.Type.Percent
                ? health + health * healthParam.Value / 100f
                : health + healthParam.Value;
            health = _maxHealth;
            HealthChanged?.Invoke(_maxHealth, health, false);
        }
        
        if (module.GameParameters.Exists(p => p.Name == "Speed"))
        {
            var speedParam = module.GameParameters
                .First(p => p.Name == "Speed");
            moveSpeed = speedParam.MeasureType == GameParameters.Type.Percent
                ? moveSpeed + moveSpeed * speedParam.Value / 100f
                : moveSpeed + speedParam.Value;
        }
        
        if (module.GameParameters.Exists(p => p.Name == "Damage"))
        {
            _damageIncreaseInPercent = module.GameParameters
                .First(p => p.Name == "Damage")
                .Value;
        }
        
        if (module.GameParameters.Exists(p => p.Name == "Armor"))
        {
            _damageReflectionInPercent = module.GameParameters
                .First(p => p.Name == "Armor")
                .Value;
        }
    }

    private void AddWeapon(Nft item)
    {
        if (item.Type is not (Type.Gun or Type.Shotgun or Type.Smg or Type.Explosive)) return;
        
        var weapons = GetAllWeapons();
        foreach (var w in weapons)
        {
            if (!string.Equals(item.Name, w.name, StringComparison.CurrentCultureIgnoreCase)) continue;

            if (item.Type.ToString() != w.GetComponent<Weapon>().weaponType.ToString()) continue;
            
            GetEquippedWeapons()[item.Type.ToString()] = w;
            break;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (_levelManager.gameIsPaused) return;
        
        _moveVector = new Vector3(
            Input.GetAxis("Horizontal"),
            0f,
            Input.GetAxis("Vertical"));
        
        _moveVector.Normalize();
        _movement.Set(moveSpeed * _moveVector.x, 0f, moveSpeed * _moveVector.z);

        var velocityZ = Vector3.Dot(_movement.normalized, transform.forward);
        var velocityX = Vector3.Dot(_movement.normalized, transform.right);
        
        _animator.SetFloat("VelocityZ", velocityZ, 0.1f, Time.deltaTime);
        _animator.SetFloat("VelocityX", velocityX, 0.1f, Time.deltaTime);
        _animator.SetBool("isMoving", _moveVector != Vector3.zero);

        if (Input.GetKeyDown(KeyCode.E))
        {
            SwitchWeapon();
        }
        
        if (Input.GetKeyDown(KeyCode.Space) && _canSprint)
        {
            _isSprinting = true;
            _canSprint = false;
            moveSpeed = sprintSpeed;
            _sprintTime = sprintDuration;
            _timeBtwSprints = 0;
            SprintCooldownStarted?.Invoke();
            _animator.SetBool("isSprinting", true);
        }

        if (_isSprinting)
        {
            if (_sprintTime > 0)
            {
                _sprintTime -= Time.deltaTime;
            }
            else
            {
                moveSpeed = _normalSpeed;
                _isSprinting = false;
                _animator.SetBool("isSprinting", false);
            }
        }

        if (_canSprint) return;
        
        if (_timeBtwSprints < sprintCooldown)
        {
            SprintCooldownContinues?.Invoke(_timeBtwSprints, sprintCooldown);
            _timeBtwSprints += Time.deltaTime;
        }
        else
        {
            SprintCooldownEnded?.Invoke();
            _canSprint = true;
        }
    }

    private void FixedUpdate()
    {
        if (_levelManager.gameIsPaused) return;
        
        _rb.velocity = _movement;
        _ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        
        if (Physics.Raycast(_ray, out _hit, 100, lookAtMask))
        {
            transform.LookAt(new Vector3(
                _hit.point.x,
                transform.position.y,
                _hit.point.z));
            
            _currentWeapon.gameObject.transform.LookAt(new Vector3(
                _hit.point.x,
                transform.position.y,
                _hit.point.z));
        }
    }

    public void ChangeHealth(float healthValue, bool damaged = true)
    {
        if (shield.activeInHierarchy && (!shield.activeInHierarchy || healthValue <= 0)) return;
        
        var newValue = damaged
            ? health + healthValue - healthValue * _damageReflectionInPercent / 100f
            : health + healthValue;
        health = newValue > _maxHealth 
            ? _maxHealth
            : newValue;

        HealthChanged?.Invoke(_maxHealth, health, damaged);

        if (health <= 0)
        {
            _animator.SetBool("dead", true);
            return;
        }

        if (damaged)
        {
            _soundManager.Groan();
        }
    }

    public void SwitchWeapon(GameObject weapon = null, bool isTaken = false)
    {
        if (_equippedWeapons.Count(w => 
                !w.Value.GetComponent<Weapon>().isUnlocked) <= 1)
            return;

        foreach (var w in _equippedWeapons
                     .Where(w => w.Value.activeInHierarchy))
        {
            w.Value.SetActive(false);
            if (_currentWeapon.fireEffect != null)
            {
                _currentWeapon.fireEffect.gameObject.SetActive(false);
            }
            if (_currentWeapon.bulletShellEffect != null)
            {
                _currentWeapon.bulletShellEffect.gameObject.SetActive(false);
            }

            if (isTaken && weapon != null)
            {
                weapon.SetActive(true);
                _currentWeapon = weapon.GetComponent<Weapon>();
                _weaponType = _currentWeapon.weaponType;
            }
            else
            {
                foreach (var t in _equippedWeapons)
                {
                    _weaponType = _weaponType.Next();
                    var nextWeapon = _equippedWeapons[_weaponType.ToString()].GetComponent<Weapon>();
                    if (nextWeapon.isUnlocked) continue;
                    
                    nextWeapon.gameObject.SetActive(true);
                    _currentWeapon = nextWeapon;
                    break;
                }
            }

            _currentWeapon.TryGetComponent<WeaponAnimationInitializer>(out var animInitializer);
            if (animInitializer != null)
                animInitializer.Set();
            
            WeaponSwitched?.Invoke(_currentWeapon);
            _soundManager.SwitchWeapon();

            break;
        }
    }

    public Weapon GetCurrentWeapon() => _currentWeapon;

    public Shield GetPlayerShield() => _shieldScript;

    private GameObject[] GetAllWeapons() => allWeapons;

    public Dictionary<string, GameObject> GetEquippedWeapons() => _equippedWeapons;

    public float GetPlayerDamageIncrease() => _damageIncreaseInPercent;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Loot"))
        {
            other.GetComponent<LootScript>().ApplyLoot(gameObject);
            Destroy(other.gameObject);
        }
    }
    
    /// <summary>
    /// Call with animation clip
    /// </summary>
    private void AttackEnded()
    {
        _currentWeapon.AttackEnded();
    }
}