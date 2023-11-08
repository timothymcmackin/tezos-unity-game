using UnityEngine;

namespace Weapons
{
    public class Bullet : MonoBehaviour
    {
        [SerializeField] protected float speed;
        [SerializeField] protected float lifetime;
        [SerializeField] protected int damage;
        [SerializeField] protected float stunTime;
    
        [SerializeField] protected LayerMask mask;

        [SerializeField] protected bool enemyBullet;

        public GameObject damageEffect;
        public GameObject destroyEffect;

        private Rigidbody _rb;
        private readonly float _distance = 1f;
        
        // Start is called before the first frame update
        protected virtual void Start()
        {
            _rb = GetComponent<Rigidbody>();
            Invoke(nameof(DestroyBullet), lifetime);
        }

        private void FixedUpdate()
        {
            _rb.velocity = transform.forward * speed;
            Physics.Raycast(transform.position, transform.forward, out var hit, _distance, mask);
            
            if (hit.collider == null) return;
            
            if (hit.collider.CompareTag("Enemy") && !enemyBullet)
            {
                hit.collider.GetComponent<Enemy>().TakeDamage(damage, stunTime);
                Instantiate(damageEffect, transform.position, Quaternion.identity);
                DestroyBullet();
                return;
            }
            
            if (hit.collider.CompareTag("Player") && enemyBullet)
            {
                hit.collider.GetComponent<PlayerController>().ChangeHealth(-damage);
                Instantiate(damageEffect, transform.position, Quaternion.identity);
                DestroyBullet();
                return;
            }
            
            Instantiate(destroyEffect, transform.position, Quaternion.identity);
            DestroyBullet();
        }

        protected virtual void DestroyBullet()
        {
            // Instantiate(destroyEffect, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
    }
}
 