using Unity.Collections;
using Unity.Networking.Transport;
using UnityEditor.PackageManager;
using UnityEngine;

struct ClientState
{
    public NetworkConnection Connection;

    public ushort SentPackets;
    public ushort LatestPacket;
    public double LatestReciveTime;
    public ushort LastEchoPacket;
}


public class ServerGame : MonoBehaviour
{
    const int MAX_CLIENTS = 16;
    const int ECHO_BUFFER = 64;

    [field: SerializeField] GameManager m_gameManager;

    NetworkDriver m_driver;
    NativeArray<ClientState> m_connections;
    NativeArray<double> m_sendTimes;

    TickTimer m_simulationTimer = new TickTimer { SecondsPerTick = 1.0/64.0, Acc = 0, Tick = 0 };
    TickTimer m_networkTimer = new TickTimer { SecondsPerTick = 1.0 / 20.0, Acc = 0, Tick = 0 };

    private void makeBuffers()
    {
        m_connections = new NativeArray<ClientState>(MAX_CLIENTS, Allocator.Persistent);
        m_sendTimes = new NativeArray<double>(MAX_CLIENTS * ECHO_BUFFER, Allocator.Persistent);
    }

    private void freeBuffers()
    {
        m_connections.Dispose();
        m_sendTimes.Dispose();
    }

    void Start()
    {
        makeBuffers();

        m_driver = NetworkDriver.Create();

        var endpoint = NetworkEndpoint.AnyIpv4.WithPort(7777);
        if (m_driver.Bind(endpoint) != 0)
        {
            Debug.LogError("Failed to bind to port 7777.");
            m_driver.Dispose();
            m_driver = default;
            return;
        }
        m_driver.Listen();
    }

    private void OnDestroy()
    {
        if (m_driver.IsCreated)
        {
            m_driver.Dispose();
        }

        freeBuffers();
    }

    private void Update()
    {
        processNetwork();

        var networkTicks = m_networkTimer.Update(Time.deltaTime);
        var simulationTicks = m_simulationTimer.Update(Time.deltaTime);

        for (int i = 0; i < simulationTicks; i++)
        {
            m_gameManager.Step((float)m_simulationTimer.SecondsPerTick);
        }

        if (networkTicks != 0)
        {
            networkTick();
        }
    }

    private void networkTick()
    {
        for (int i = 0; i < m_connections.Length; i++)
        {
            var client = m_connections[i];
            if (client.Connection.IsCreated)
            {
                m_driver.BeginSend(client.Connection, out var writer);
                writer.WriteUShort(client.SentPackets);
                if(client.LatestPacket == client.LastEchoPacket)
                {
                    writer.WriteUShort(0);
                }
                else
                {
                    writer.WriteUShort(client.LatestPacket);
                    writer.WriteDouble(Time.realtimeSinceStartupAsDouble - client.LatestReciveTime);
                }
                m_sendTimes[client.SentPackets % ECHO_BUFFER + i * ECHO_BUFFER] = Time.realtimeSinceStartupAsDouble;
                m_driver.EndSend(writer);
                client.SentPackets += 1;
                m_connections[i] = client;
            }
        }
    }


    private int getFreeConnection()
    {
        for(int i = 0; i < m_connections.Length; i++)
        {
            if (!m_connections[i].Connection.IsCreated)
            {
                return i;
            }
        }

        return -1;
    }

    private void processConnect(NetworkConnection connection)
    {
        int freeId = getFreeConnection();
        if (freeId == -1)
        {
            m_driver.Disconnect(connection);
        }
        else
        {
            m_gameManager.AddBall(freeId);
            m_connections[freeId] = new ClientState { Connection = connection, SentPackets = 0, LatestPacket = 0, LatestReciveTime = 0, LastEchoPacket = 0};
        }
    }

    private void processDisconnect(int id, NetworkConnection connection)
    {
        m_gameManager.RemoveBall(id);
        m_connections[id] = new ClientState { Connection = default };
    }

    private void processData(int id, ref DataStreamReader reader)
    {
        var packetNumber = reader.ReadUShort();
        var echoNumber = reader.ReadUShort();
        if(echoNumber != 0)
        {
            //Debug.Log($"Client Echoed: {echoNumber}");
            var echoHold = reader.ReadDouble();
            //Debug.Log($"Client Held: {echoHold}");

            if( echoNumber + ECHO_BUFFER > m_connections[id].SentPackets)
            {
                //Debug.Log($"Client Ping: {Time.realtimeSinceStartupAsDouble - m_sendTimes[echoNumber % ECHO_BUFFER + id * ECHO_BUFFER] - echoHold}");
            }
        }

        if(packetNumber >= m_connections[id].LatestPacket)
        {
            var copy = m_connections[id];
            copy.LatestPacket = packetNumber;
            copy.LatestReciveTime = Time.realtimeSinceStartupAsDouble;
            m_connections[id] = copy;
        }
        
    }

    private void processNetwork()
    {
        m_driver.ScheduleUpdate().Complete();
        NetworkConnection newConnection;
        while ((newConnection = m_driver.Accept()) != default)
        {
            processConnect(newConnection);
        }

        for (int i = 0; i < m_connections.Length; i++)
        {
            DataStreamReader stream;
            NetworkEvent.Type cmd;

            if(m_connections[i].Connection.IsCreated)
            {
                while ((cmd = m_driver.PopEventForConnection(m_connections[i].Connection, out stream)) != NetworkEvent.Type.Empty)
                {
                    if (cmd == NetworkEvent.Type.Data)
                    {
                       processData(i, ref stream);
                    }
                    else if (cmd == NetworkEvent.Type.Disconnect)
                    {
                        processDisconnect(i, m_connections[i].Connection);
                        break;
                    }

                }
            }
           
        }

    }
        
        







}
