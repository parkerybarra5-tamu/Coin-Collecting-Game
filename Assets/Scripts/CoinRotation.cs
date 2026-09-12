using UnityEngine;

public class CoinRotation : MonoBehaviour
{
    [field: SerializeField] float m_rotationSpeed;
    [field: SerializeField] float m_bobSpeed;
    [field: SerializeField] float m_bobHeight;
    [field: SerializeField] Transform m_target;
    Vector3 m_targetPosition;
    float m_t;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SetTarget(m_target);
    }

    // Update is called once per frame
    void Update()
    {
        m_t += Time.deltaTime;

        if(m_target != null)
        {
            var rot = m_target.rotation.eulerAngles;
            rot.y += m_rotationSpeed * Time.deltaTime;
            m_target.rotation = Quaternion.Euler(rot);
            m_target.position = m_targetPosition + Vector3.up * Mathf.Sin(m_t * m_bobSpeed) * m_bobHeight;
        }
    }

    public void SetTarget(Transform target)
    {
        if(target == null)
        {
            m_target = null;
            return;
        }

        m_targetPosition = target.position;
        m_target = target;
    }
}
