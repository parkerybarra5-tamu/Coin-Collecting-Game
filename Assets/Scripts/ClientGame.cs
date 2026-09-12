using Unity.Collections;
using Unity.Mathematics;
using Unity.Networking.Transport;
using UnityEngine;

public class ClientGame : MonoBehaviour
{

    private void OnGUI()
    {
        GUILayout.TextArea($"Client Ping: {m_ping}");
        GUILayout.TextArea($"Client Jitter: {m_jitter}");
    }

    const int ECHO_BUFFER = 64;


    NetworkDriver m_driver;
    NetworkConnection m_connection;
    NativeArray<double> m_sendTimes = new NativeArray<double>(ECHO_BUFFER, Allocator.Persistent);
    
    TickTimer m_simulationTimer = new TickTimer { SecondsPerTick = 1.0 / 64.0, Acc = 0, Tick = 0 };

    ushort m_sentPackets = 0;
    ushort m_latestServerPacket = 0;
    double m_latestServerPacketReciveTime = 0;
    ushort m_latestEchoPacket = 0;
    double m_ping = 0;
    double m_lastPing = 0;
    double m_jitter = 0;

    private void makeBuffers()
    { 
        m_sendTimes = new NativeArray<double>(ECHO_BUFFER, Allocator.Persistent);
    }

    private void freeBuffers()
    {
        m_sendTimes.Dispose();
    }

    private void Start()
    {
        makeBuffers();

        m_driver = NetworkDriver.Create();

        var endpoint = NetworkEndpoint.LoopbackIpv4.WithPort(7777);
        m_connection = m_driver.Connect(endpoint);
    }

    void OnDestroy()
    {
        if (m_connection.IsCreated)
        {
            m_driver.Disconnect(m_connection);
        }
        m_driver.Dispose();

        freeBuffers();
    }

    private void processConnect()
    {
    
    }

    private void processDisconnect()
    {
        m_connection = default;
    }


    private void processData(ref DataStreamReader reader)
    {
        var packetNumber = reader.ReadUShort();
        var echoNumber = reader.ReadUShort();
        if (echoNumber != 0)
        {
            Debug.Log($"Client Echoed: {echoNumber}");
            var echoHold = reader.ReadDouble();
            Debug.Log($"Client Held: {echoHold}");

            if (echoNumber + ECHO_BUFFER > m_sentPackets)
            {
                var ping = Time.realtimeSinceStartupAsDouble - m_sendTimes[echoNumber % ECHO_BUFFER] - echoHold;
                m_ping = ping;
                if(m_lastPing != 0)
                {
                    m_jitter = math.square(ping - m_lastPing);
                }
                m_lastPing = ping;
                Debug.Log($"Client Ping: {Time.realtimeSinceStartupAsDouble - m_sendTimes[echoNumber % ECHO_BUFFER] - echoHold}");
            }
        }
        if (packetNumber > m_latestServerPacketReciveTime)
        {
            m_latestServerPacket = packetNumber;
            m_latestServerPacketReciveTime = Time.realtimeSinceStartupAsDouble;
        }
       

    }



    private void processNetwork()
    {
        m_driver.ScheduleUpdate().Complete();

        if (!m_connection.IsCreated)
        {
            return;
        }

        DataStreamReader stream;
        NetworkEvent.Type cmd;
        while ((cmd = m_connection.PopEvent(m_driver, out stream)) != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                processConnect();
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                processData(ref stream);
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                processDisconnect();
            }
        }
    }


    private void networkTick()
    {
        if(m_driver.BeginSend(m_connection, out var writer) != 0)
        {
            Debug.Log("Failed to begin send!");
            return;
        }
        
        writer.WriteUShort(m_sentPackets);
        if (m_latestEchoPacket == m_latestServerPacket)
        {
            writer.WriteUShort(0);
        }
        else
        {
            writer.WriteUShort(m_latestServerPacket);
            writer.WriteDouble(Time.realtimeSinceStartupAsDouble - m_latestServerPacketReciveTime);
            m_latestEchoPacket = m_latestServerPacket;
        }
        m_sendTimes[m_sentPackets%ECHO_BUFFER] = Time.realtimeSinceStartupAsDouble;
        m_driver.EndSend(writer);
        m_sentPackets++;
    }


    private void Update()
    {


        processNetwork();

        var simulationStartTick = m_simulationTimer.Tick;
        var simulationTicks = m_simulationTimer.Update((float)Time.deltaTime);

        for (var i = simulationStartTick; i < m_simulationTimer.Tick; i++)
        {
            
        }

        if (simulationTicks != 0)
        {
            if (m_connection.IsCreated)
            {
                networkTick();
            }
        }
    }

   

}
