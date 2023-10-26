using System;
using Managers;
using UnityEngine;

namespace Weapons
{
    public class Weapon : MonoBehaviour
    {
        public WeaponPurpose weaponPurpose;
        public WeaponType weaponType;
        [SerializeField] protected GameObject bullet;
        [SerializeField] protected Transform shotPoint;
    
        [SerializeField] protected float fireRate;
        [SerializeField] protected int magazineCapacity;
        [SerializeField] protected float reloadTime;
        protected float timeBtwShots;
        protected bool reloading;
        protected float timeBtwReloading;
        protected int ammoQtyInMagazine;
        protected int ammo;

        public enum WeaponPurpose
        {
            Player,
            Enemy
        }
    
        public enum WeaponType
        {
            Default,
            Gun,
            Shotgun,
            Mortar
        }
    
        public Action<int, int> ammoQtyChanged;

        protected SoundManager soundManager;
    
        // Start is called before the first frame update
        void Start()
        {
            soundManager = GameObject.FindGameObjectWithTag("Manager")
                .GetComponent<SoundManager>();
            
            ReloadAmmo();
        }

        // Update is called once per frame
        void Update()
        {
            if (timeBtwShots <= 0)
            {
                if (Input.GetMouseButton(0) && weaponPurpose == WeaponPurpose.Player && ammoQtyInMagazine > 0 && !reloading
                    || Input.GetMouseButton(0) && weaponType == WeaponType.Default 
                    || weaponPurpose == WeaponPurpose.Enemy)
                {
                    Shoot(); 
                }
            }
            else
            {
                timeBtwShots -= Time.deltaTime;
            }
            
            if (Input.GetKeyDown(KeyCode.R) && 
                ammoQtyInMagazine < magazineCapacity &&
                ammo > 0 &&
                !reloading)
            {
                reloading = true;
                soundManager.Reload();
                timeBtwReloading = reloadTime;
            }
            
            if (reloading)
            {
                ReloadAmmo();
            }
        }

        protected void ReloadAmmo()
        {
            timeBtwReloading -= Time.deltaTime;
            
            if (!(timeBtwReloading <= 0)) return;

            if (ammoQtyInMagazine == magazineCapacity) return;
            
            var allAmmo = ammoQtyInMagazine + ammo;
            if (allAmmo >= magazineCapacity)
            {
                ammo = ammo - magazineCapacity + ammoQtyInMagazine; 
                ammoQtyInMagazine = magazineCapacity;
            }
            else
            {
                ammoQtyInMagazine = allAmmo;
                ammo = 0;
            }

            reloading = false;
            ammoQtyChanged?.Invoke(ammoQtyInMagazine, ammo);
        }

        protected virtual void Shoot()
        {
            if (weaponType != WeaponType.Shotgun)
                Instantiate(bullet, shotPoint.position, shotPoint.rotation);
        
            soundManager.Shot(weaponType);
            timeBtwShots = fireRate;
        
            if (weaponType == WeaponType.Default || weaponPurpose == WeaponPurpose.Enemy) 
                return;
        
            ammoQtyInMagazine--;
            ammoQtyChanged?.Invoke(ammoQtyInMagazine, ammo);
        }

        public (int, int) GetAmmo()
        {
            return (ammoQtyInMagazine, ammo);
        }

        public void ChangeAmmoQty(int qty)
        {
            ammo += qty;
            ammoQtyChanged?.Invoke(ammoQtyInMagazine, ammo);;
        }
    }
}