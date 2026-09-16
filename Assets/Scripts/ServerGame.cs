using System;
using Unity.Collections;
using Unity.Networking.Transport;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Windows;

struct ClientState
{
    public NetworkConnection Connection;
    public ushort SentPackets;
    public ushort LatestPacket;
    public double LatestReciveTime;
    public ushort LastEchoedPacket;
    public ushort LastestRecivedInputTick;

}



public class ServerGame : MonoBehaviour
{
    const int MAX_CLIENTS = 16;
    const int ECHO_BUFFER = 64;
    const int INPUT_BUFFER = 128;
    private void OnGUI()
    {
        String s = "";
        for(int i = 0; i<INPUT_BUFFER;i++)
        {
            short diff = (short)(m_connectionInputHistories[i].tick - m_simulationTimer.Tick);
            if (diff > 32 || diff < -95)
            {
                s += '0';
            }
            else if (diff == 0)
                {
                s += '*';
            }
            else
            {
                s += '1';
            }
        }
        GUILayout.TextArea(s);
    }


    [field: SerializeField] GameManager m_gameManager;

    NetworkDriver m_driver;
    NativeArray<ClientState> m_connections;
    NativeArray<BallInput> m_connectionInputHistories;
    NativeArray<double> m_sendTimesHistory;

    TickTimer m_simulationTimer = new TickTimer { SecondsPerTick = 1.0/64.0, Acc = 0, Tick = 0 };
    TickTimer m_networkTimer = new TickTimer { SecondsPerTick = 1.0 / 20.0, Acc = 0, Tick = 0 };

    void Start()
    {

        m_connections = new NativeArray<ClientState>(MAX_CLIENTS, Allocator.Persistent);
        m_sendTimesHistory = new NativeArray<double>(MAX_CLIENTS * ECHO_BUFFER, Allocator.Persistent);
        m_connectionInputHistories = new NativeArray<BallInput>(INPUT_BUFFER * MAX_CLIENTS, Allocator.Persistent);

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

        m_gameManager.AddCoins();
    }

    private void OnDestroy()
    {
        if (m_driver.IsCreated)
        {
            m_driver.Dispose();
        }

        m_connections.Dispose();
        m_sendTimesHistory.Dispose();
        m_connectionInputHistories.Dispose();
    }
    void setConnectionInput(byte id, ushort tick)
    {
        m_gameManager.SetBallInput(id, m_connectionInputHistories[(tick % INPUT_BUFFER) * MAX_CLIENTS + id]);
    }
    
    private void Update()
    {
        processNetwork();

        var st = m_simulationTimer.Tick;
        var networkTicks = m_networkTimer.Update(Time.deltaTime);
        var simulationTicks = m_simulationTimer.Update(Time.deltaTime);

        for (var i = st; i < m_simulationTimer.Tick; i++)
        {
            foreachConnectedClient(id => setConnectionInput(id, i));
            m_gameManager.Step((float)m_simulationTimer.SecondsPerTick);
        }

        if (networkTicks != 0)
        {
            foreachConnectedClient(sendClientPacket);
        }
    }

    private void foreachConnectedClient(Action<byte> clientAction)
    {
        for (byte i = 0; i < m_connections.Length; i++)
        {
            if (m_connections[i].Connection.IsCreated)
            {
                clientAction.Invoke(i);    
            }
        }
    }

    private void sendClientPacket(byte id)
    {
        var client = m_connections[id];

        m_driver.BeginSend(client.Connection, out var writer);
        ushort echoNum = (client.LatestPacket != client.LastEchoedPacket) ? client.LatestPacket : (ushort)0;
        client.LastEchoedPacket = client.LatestPacket;
        EchoPacket echoPacket = new EchoPacket { packetNumber = echoNum, holdTime = Time.realtimeSinceStartupAsDouble - client.LatestReciveTime };
        CommonPacket commonPacket = new CommonPacket { packetNumber = client.SentPackets, echoPacket = echoPacket };
        commonPacket.Write(ref writer);
        new TickTimerPacket(m_simulationTimer).Write(ref writer);

        writer.WriteUShort(m_connections[id].LastestRecivedInputTick);
        


        var copy = writer;
        writer.WriteByte(0);
        byte size = 0;
        for (int j = 0; j < MAX_CLIENTS; j++)
        {
            //writer.WriteByte((byte)(m_connections[j].Connection.IsCreated?1:0));
            if (m_connections[j].Connection.IsCreated)
            {
                size++;
                writer.WriteByte((byte)j);

                var pos = m_gameManager.GetBallTransform(j);
                writer.WriteFloat(pos.position.x);
                writer.WriteFloat(pos.position.y);
                writer.WriteFloat(pos.position.z);
            }
        }
        copy.WriteByte(size);
        m_sendTimesHistory[client.SentPackets % ECHO_BUFFER + id * ECHO_BUFFER] = Time.realtimeSinceStartupAsDouble;
        m_driver.EndSend(writer);
        client.SentPackets += 1;
        m_connections[id] = client;

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
                if(client.LatestPacket == client.LastEchoedPacket)
                {
                    writer.WriteUShort(0);
                }
                else
                {
                    writer.WriteUShort(client.LatestPacket);
                    writer.WriteDouble(Time.realtimeSinceStartupAsDouble - client.LatestReciveTime);
                }

  

                writer.WriteUShort(m_simulationTimer.Tick);
                writer.WriteDouble(m_simulationTimer.Acc);

                var copy = writer;
                writer.WriteByte(0);
                byte size = 0;
                for (int j = 0; j < MAX_CLIENTS; j++)
                {
                    //writer.WriteByte((byte)(m_connections[j].Connection.IsCreated?1:0));
                    if (m_connections[j].Connection.IsCreated)
                    {
                        size++;
                        writer.WriteByte((byte)j);

                        var pos = m_gameManager.GetBallTransform(j);
                        writer.WriteFloat(pos.position.x);
                        writer.WriteFloat(pos.position.y);
                        writer.WriteFloat(pos.position.z);
                    }
                }
                copy.WriteByte(size);
                m_sendTimesHistory[client.SentPackets % ECHO_BUFFER + i * ECHO_BUFFER] = Time.realtimeSinceStartupAsDouble;
                m_driver.EndSend(writer);
                client.SentPackets += 1;
                m_connections[i] = client;
            }
        }
    }


    private int getFreeConnection()
    {
        for (int i = 0; i < m_connections.Length; i++)
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
            m_connections[freeId] = new ClientState { Connection = connection, SentPackets = 0, LatestPacket = 0, LatestReciveTime = 0, LastEchoedPacket = 0};
        }
    }

    private void processDisconnect(int id, NetworkConnection connection)
    {
        m_gameManager.RemoveBall(id);
        m_connections[id] = new ClientState { Connection = default };
    }

    private void setInput(int id, BallInput input)
    {
        // Casting the difference to short handles uint16/ushort overflow and wrap-around correctly
        short diff = (short)(input.tick - m_simulationTimer.Tick);

        // Rejects inputs more than 16 ticks in the future or more than 47 ticks in the past
        if (diff > 32 || diff < -95)
        {
            return;
        }

        // Process valid input
        m_connectionInputHistories[(input.tick%INPUT_BUFFER)*MAX_CLIENTS + id] = input;
        if (m_connections[id].LastestRecivedInputTick<input.tick)
        {
            var copy = m_connections[id];
            copy.LastestRecivedInputTick = input.tick;
            m_connections[id] = copy;
        }
    }

    private void processData(int id, ref DataStreamReader reader)
    {
        //Debug.Log($"recived: {reader.Length}");
     
        CommonPacket packet = new CommonPacket(ref reader);
      
 

        var inputPacket = new CompressedInputPacket(ref reader);
        //Debug.Log($"Encoded Inputs: {inputPacket.inputs.Length}");
        foreach (var i in inputPacket.inputs)
        {
            setInput(id, i);
        }

        if(packet.packetNumber >= m_connections[id].LatestPacket)
        {
            var copy = m_connections[id];
            copy.LatestPacket = packet.packetNumber;
            copy.LatestReciveTime = Time.realtimeSinceStartupAsDouble;
            m_connections[id] = copy;
        }
        
    }

    private void processClient(byte id)
    {
        DataStreamReader stream;
        NetworkEvent.Type cmd;

        while ((cmd = m_driver.PopEventForConnection(m_connections[id].Connection, out stream)) != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Data)
            {
                processData(id, ref stream);
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                processDisconnect(id, m_connections[id].Connection);
                break;
            }

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

        foreachConnectedClient(processClient);
    }
}
