using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{

    const int max_balls = 16;
    [field: SerializeField] GameObject ball_prefab;
    [field: SerializeField] GameObject coin_prefab;
    [field: SerializeField] Transform[] coin_spawns;

    BallPhysics[] m_balls = new BallPhysics[max_balls];
    List<CoinPhysics> m_coins = new List<CoinPhysics>();

    private void Start()
    {
        BallPhysics tempBall = Instantiate(ball_prefab).GetComponent<BallPhysics>();
        if(tempBall == null)
        {
            Debug.LogError("Failed to find Ball Physics Component");
            Destroy(tempBall.gameObject);
            Destroy(gameObject);
            return;
        }
        Destroy(tempBall.gameObject);

        CoinPhysics tempCoin = Instantiate(coin_prefab).GetComponent<CoinPhysics>();
        if (tempCoin == null)
        {
            Debug.LogError("Failed to find Coin Physics Component");
            Destroy(tempCoin.gameObject);
            Destroy(gameObject);
            return;
        }
        Destroy(tempCoin.gameObject);
    }

    public int GetCoinCount()
    {
        return m_coins.Count;
    }

    public void AddCoins()
    {
        foreach(Transform spawn in coin_spawns)
        {
            m_coins.Add(Instantiate(coin_prefab, spawn, false).GetComponent<CoinPhysics>().SetGameManager(this));
        }
    }

    public void RemoveCoin(CoinPhysics coin, BallPhysics ball)
    {
        m_coins.Remove(coin);
        Destroy(coin.gameObject);
    }

    public void AddBall(int id)
    {
        if (m_balls[id] == null)
        {
            m_balls[id] = Instantiate(ball_prefab, transform).GetComponent<BallPhysics>(); // should allways find component, checked at start;
            return;
        }
        Debug.LogWarning("AddBall(int) called for invalid id!");
    }

    public void RemoveBall(int id)
    {
        if (m_balls[id] != null)
        {
            var copy = m_balls[id];
            m_balls[id] = null;
            Destroy(copy.gameObject);
            return;
        }
        Debug.LogWarning("RemoveBall(int) called for invalid id!");
    }

    public void SetBallInput(int id, BallInput input)
    {
        if (m_balls[id] != null)
        {
            m_balls[id].SetBallInput(input);
            return;
        }
        Debug.LogWarning("SetBallInput(int, BallInput) called for non-exisitent id!");
    }

    public Transform GetBallTransform(int id)
    {
        if (m_balls[id]!=null)
        {
            return m_balls[id].transform;
        }
        return null;
    }

    public void Step(float dt)
    {
        foreach(BallPhysics ball in m_balls)
        {
            if(ball == null)
            {
                continue;
            }
            ball.Step(dt);
        }
        Physics.Simulate(dt);
    }
}
