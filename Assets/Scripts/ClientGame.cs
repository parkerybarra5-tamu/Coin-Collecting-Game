using System;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.InputSystem;

struct BallState
{
    public byte mask;
    public ushort tick;
    public Vector3 position;
    public byte id;

}

struct RenderState
{
    public BallState from;
    public BallState to;
}

public struct CommonPacket
{
    public ushort packetNumber;
    public EchoPacket echoPacket;

    public CommonPacket(ref DataStreamReader reader)
    {
        packetNumber = reader.ReadUShort();
        echoPacket = new EchoPacket(ref reader);
    }

    public void Write(ref DataStreamWriter writer)
    {
        writer.WriteUShort(packetNumber);
        echoPacket.Write(ref writer);
    }
}

public struct EchoPacket
{
    public ushort packetNumber;
    public double holdTime;

    public EchoPacket(ref DataStreamReader reader)
    {
        packetNumber = reader.ReadUShort();
        holdTime = 0;
        if(packetNumber != 0)
        {
            holdTime = reader.ReadDouble();
        }
    }

    public void Write(ref DataStreamWriter writer)
    {
        writer.WriteUShort(packetNumber);
        if(packetNumber != 0)
        {
            writer.WriteDouble(holdTime);
        }
    }
}
public struct DeltaInputPacket
{
    public BallInput ballInput;
    public DeltaInputPacket(ref DataStreamReader reader, BallInput full)
    {
        var mask = reader.ReadByte();
        if((mask&1) != 0)
        {
            full.direction.x = reader.ReadFloat();
        }
        if ((mask & 2) != 0)
        {
            full.direction.y = reader.ReadFloat();
        }
        full.jump = (mask & 4) != 0;
        full.tick--;
        ballInput = full;

    }

    public void Write(ref DataStreamWriter writer, BallInput to)
    {
        byte mask = 0;
        var copy = writer;
        writer.WriteByte(0);

        if (ballInput.direction.x != to.direction.x)
        {
            mask |= 1;
            writer.WriteFloat(ballInput.direction.x);
        }
        if (ballInput.direction.y != to.direction.y)
        {
            mask |= 2;
            writer.WriteFloat(ballInput.direction.y);
        }
        mask |= (byte)(ballInput.jump ? 4 : 0);
        copy.WriteByte(mask);
    }
}
public struct FullInputPacket
{
    public BallInput ballInput;

    public FullInputPacket(ref DataStreamReader reader, ushort tick)
    {
        ballInput = new BallInput { tick = tick, direction = new Vector2(reader.ReadFloat(), reader.ReadFloat()), jump = (reader.ReadByte() == 1) ? true : false };
    }

    public void Write(ref DataStreamWriter writer)
    {
        writer.WriteFloat(ballInput.direction.x);
        writer.WriteFloat(ballInput.direction.y);
        writer.WriteByte(ballInput.jump ? (byte)1 : (byte)0);
    }
}

public struct CompressedInputPacket
{
    public NativeArray<BallInput> inputs;
    public ushort lastTick;

    public CompressedInputPacket(NativeArray<BallInput> inputBuf, ushort inputEnd, ushort serverAck)
    {
        int cur = inputEnd;
        int count = 0;
        do
        {
            count++;
            cur -= 1;
        } while (cur % inputBuf.Length != inputEnd % inputBuf.Length && cur >= serverAck);

        inputs = new NativeArray<BallInput>(count, Allocator.Temp);
        lastTick = inputEnd;

        int x = 0;
        for (int i = cur+1; i <= inputEnd; i++)
        {
            inputs[x] = inputBuf[i%inputBuf.Length];
            x++;
        }
    }
    public CompressedInputPacket(ref DataStreamReader reader)
    {
        byte count = reader.ReadByte();
        lastTick = reader.ReadUShort();
        inputs = new NativeArray<BallInput>(count, Allocator.Temp);
        if (count != 0)
        {
            inputs[count - 1] = new FullInputPacket(ref reader, lastTick).ballInput;
            count--;
            while (count > 0)
            {
                inputs[count-1] = new DeltaInputPacket(ref reader, inputs[count]).ballInput;
                count--;
            }
        }
    }

    public void Write(ref DataStreamWriter write)
    {
        write.WriteByte((byte)inputs.Length);
        write.WriteUShort(lastTick);
        if (inputs.Length != 0)
        {
            (new FullInputPacket { ballInput = inputs[inputs.Length - 1] }).Write(ref write);
            for (int i = inputs.Length - 1; i > 0;  i--)
            {
                (new DeltaInputPacket { ballInput = inputs[i - 1] }).Write(ref write, inputs[i]);
            }
        }
    }
}



struct FullBallPacket
{
    public byte id;
    public Vector3 position;

    public FullBallPacket(ref DataStreamReader reader)
    {
        id = reader.ReadByte();
        position = new Vector3(reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
    }

    public void Write(ref DataStreamWriter writer)
    {
        writer.WriteByte(id);
        writer.WriteFloat(position.x);
        writer.WriteFloat(position.y);
        writer.WriteFloat(position.z);
    }
}


struct TickTimerPacket
{
    public TickTimer timer;

    public TickTimerPacket(TickTimer _timer)
    {
        timer = _timer;
    }

    public TickTimerPacket(ref DataStreamReader reader, double tps)
    {
        timer = new TickTimer { SecondsPerTick = tps, Tick = reader.ReadUShort(), Acc = reader.ReadDouble() };
    }

    public void Write(ref DataStreamWriter writer)
    {
        writer.WriteUShort(timer.Tick);
        writer.WriteDouble(timer.SecondsPerTick);
    }
}

public interface IFoldable<R, T>
{
    public T Fold(R l);
}

public struct CircularBufferIterator<T> where T : struct 
{
    NativeArray<T> src;
    public int position;
    public int index => position % src.Length;

    public CircularBufferIterator(NativeArray<T> src)
    {
        this.src = src;
        position = 0;
    }


    public R ForWrittenFromZeroFold<R>(R acc) where R : struct, IFoldable<T,R>
    {
        if (position >= src.Length)
        {
            foreach (var item in src)
            {
                acc = acc.Fold(item);
            }
            return acc;
        }

        for(int i = 0; i < position; i++)
        {
            acc = acc.Fold(src[i]);
        }
        return acc;
    }

    public void Push(T item)
    {
        src[index] = item;
        position++;
    }
}

struct FoldableInt : IFoldable<int, FoldableInt>
{
    public int Value;

    public FoldableInt Fold(int l)
    {
        int v = Value + l;
        return new FoldableInt { Value = v };
    }
}

struct PingAcc : IFoldable<double, PingAcc>
{
    public double Sum;
    public int Count;

    public PingAcc Fold(double l)
    {
        double v = Sum + l;
        int c = Count+1;
        
        return new PingAcc { Sum = v, Count = c};
    }

    public double GetPing()
    {
        return Sum / Count;
    }
}

struct JitterAcc : IFoldable<double, JitterAcc>
{
    public bool IsFirst;
    public double Last;
    public double Sum;
    public int Count;

    public JitterAcc Fold(double l)
    {
        double v = 0;
        int c = 0;
        if(!IsFirst)
        {
            v = math.square(l - Last);
            c = Count + 1;
        }
        return new JitterAcc { Sum = v, Count = c, Last = l, IsFirst = false };
    }

    public double GetJitter()
    {
        return math.sqrt(Sum / Count);
    }
}



public class ClientGame : MonoBehaviour
{

    private void OnGUI()
    {
        var pingAcc = new PingAcc { Count = 0, Sum = 0 };
        var result = m_pingHistoryIterator.ForWrittenFromZeroFold(pingAcc);
        GUILayout.TextArea($"{result.GetPing()}");
        GUILayout.TextArea($"{result.Count}");
        GUILayout.TextArea($"{m_pingHistoryIterator.ForWrittenFromZeroFold(new JitterAcc { IsFirst = true }).GetJitter()}");
    }
    const int ECHO_BUFFER = 64;
    const int INPUT_BUFFER = 16;
    const int STATE_BUFFER = 128;
    const byte MAX_CLIENTS = 16;

    [SerializeField] InputAction m_ballDirection;
    [SerializeField] InputAction m_ballJump;
    [SerializeField] SimpleThirdPersonCamera m_cam;
    [SerializeField] GameObject Ballprefabview;
    [SerializeField] Vector3 poffset;

    Transform[] m_ballViews = new Transform[16];

    NetworkDriver m_driver;
    NetworkConnection m_serverConnection;
    NativeArray<double> m_sendTimeHistory;
    NativeArray<BallInput> m_predictionInputHistory;
    NativeArray<BallState> m_ballStateHistory;
    NativeArray<double> m_pingHistory;
    CircularBufferIterator<double> m_pingHistoryIterator;

    TickTimer m_predictionTimer = new TickTimer { SecondsPerTick = 1.0 / 64.0, Acc = 0, Tick = 0 };
    TickTimer m_interpolationTimer = new TickTimer { SecondsPerTick = 1.0 / 64, Acc = 0, Tick = 0 };

    ushort m_latestStatetick;
    ushort m_sentPackets = 0;
    ushort m_latestServerPacket = 0;
    double m_latestServerReciveTime = 0;
    ushort m_latestEchoedPacket = 0;

    ushort m_latestInputAck;

    private void makeBuffers()
    { 
        m_sendTimeHistory = new NativeArray<double>(ECHO_BUFFER, Allocator.Persistent);
        m_predictionInputHistory = new NativeArray<BallInput>(INPUT_BUFFER, Allocator.Persistent);
        m_ballStateHistory = new NativeArray<BallState>(STATE_BUFFER*MAX_CLIENTS, Allocator.Persistent);
        m_renderStates = new NativeArray<RenderState>(MAX_CLIENTS, Allocator.Persistent) ;
        m_pingHistory = new NativeArray<double>(64, Allocator.Persistent);
    }

    private void freeBuffers()
    {
        m_sendTimeHistory.Dispose();
        m_predictionInputHistory.Dispose();
        m_ballStateHistory.Dispose();
        m_renderStates.Dispose();
        m_pingHistory.Dispose();
    }


    ushort GetMinValidTick(in NativeArray<BallState> states, int clientId, ushort centerTick, int stateBuffer)
{
    ushort minTick = 0;
    bool found = false;
    int baseIndex = clientId * stateBuffer;

    for (int i = 0; i < stateBuffer; i++)
    {
        var cur = states[baseIndex + i];
        if (cur.tick == 0) continue;

        // Check if within valid window and smaller than current minTick (handles ushort wrap-around)
        if (math.abs((short)(cur.tick - centerTick)) <= stateBuffer / 2)
        {
            if (!found || (short)(cur.tick - minTick) < 0)
            {
                minTick = cur.tick;
                found = true;
            }
        }
    }

    return minTick; // Returns 0 if no valid tick found in window
}



    private void Start()
    {
        makeBuffers();
        m_pingHistoryIterator = new CircularBufferIterator<double>(m_pingHistory);

        m_driver = NetworkDriver.Create();

        var endpoint = NetworkEndpoint.LoopbackIpv4.WithPort(7777);
        m_serverConnection = m_driver.Connect(endpoint);

        for (int i = 0; i < MAX_CLIENTS; i++)
        {
            m_ballViews[i] = Instantiate(Ballprefabview, transform).transform;
            m_ballViews[i].gameObject.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (m_serverConnection.IsCreated)
        {
            m_driver.Disconnect(m_serverConnection);
        }
        m_driver.Dispose();

        freeBuffers();
    }

    private void OnEnable()
    {
        m_ballDirection.Enable();
        m_ballJump.Enable();
    }

    private void OnDisable()
    {
        m_ballDirection.Disable();
        m_ballJump.Disable();
    }
    
    private void processConnect()
    {
    
    }

    private void processDisconnect()
    {
        m_serverConnection = default;
    }


    private void processData(ref DataStreamReader reader)
    {
   
        CommonPacket packet = new CommonPacket(ref reader);
        if (packet.echoPacket.packetNumber != 0)
        {
            if (packet.echoPacket.packetNumber <= m_sentPackets && packet.echoPacket.packetNumber > m_sentPackets - ECHO_BUFFER)
            {
                var ping = Time.realtimeSinceStartupAsDouble - m_sendTimeHistory[packet.echoPacket.packetNumber % ECHO_BUFFER] - packet.echoPacket.holdTime;
                m_pingHistoryIterator.Push(ping);
            }
            else
            {
                Debug.LogWarning("Client Missed Ping: too late");
            }
        }
        if (packet.packetNumber > m_latestServerReciveTime)
        {
            m_latestServerPacket = packet.packetNumber;
            m_latestServerReciveTime = Time.realtimeSinceStartupAsDouble;

        }
       
        TickTimerPacket tp = new TickTimerPacket(ref reader, 1.0/64.0);
        m_latestInputAck = reader.ReadUShort();
        var serverTime = tp.timer;
        if (m_latestStatetick < serverTime.Tick)
        {
            m_latestStatetick = serverTime.Tick;
        }

        var pingAcc = new PingAcc { Count = 0, Sum = 0 };
        var result = m_pingHistoryIterator.ForWrittenFromZeroFold(pingAcc);
        //var rawtt = serverTime.TotalTime();
        serverTime.Acc += result.GetPing() / 2;
        serverTime.RestoreAcc();
        var renderTime = serverTime;

        //Debug.Log($"SV:{rawtt}, C:{serverTime.TotalTime()}");


        serverTime.Tick += 2;
        if (math.abs(serverTime.TotalTime() - m_predictionTimer.TotalTime()) > m_predictionTimer.SecondsPerTick)
        {
            Debug.LogWarning("Prediction Target Missmatch");
            m_predictionTimer.Tick = serverTime.Tick;
            m_predictionTimer.Acc = serverTime.Acc;
        }

       
        renderTime.Tick -= 2;
        if (math.abs(m_interpolationTimer.TotalTime() - renderTime.TotalTime()) > m_predictionTimer.SecondsPerTick)
        {
            Debug.LogWarning("Render Target Missmatch");
            m_interpolationTimer.Tick = renderTime.Tick;
            m_interpolationTimer.Acc = renderTime.Acc;
        }
       
  

        for (int i = 0; i < MAX_CLIENTS; i++)
        {
            m_ballViews[i].gameObject.SetActive(false);
        }

        byte size = reader.ReadByte();
        for (int i = 0; i < size; i++)
        {
            var ballPacket = new FullBallPacket(ref  reader);
           
            m_ballViews[ballPacket.id].gameObject.SetActive(true);
            var bs = new BallState { id = ballPacket.id, position = ballPacket.position + poffset, mask = 1, tick = serverTime.Tick };
            addState(bs);
            m_ballViews[i].position = bs.position;
        }

     

     
       
   
    


       

    }



    private void processNetwork()
    {
        m_driver.ScheduleUpdate().Complete();

        if (!m_serverConnection.IsCreated)
        {
            return;
        }

        DataStreamReader stream;
        NetworkEvent.Type cmd;
        while ((cmd = m_serverConnection.PopEvent(m_driver, out stream)) != NetworkEvent.Type.Empty)
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
        if(m_driver.BeginSend(m_serverConnection, out var writer) != 0)
        {
            Debug.Log("Failed to begin send!");
            return;
        }

        ushort echoNum = (m_latestServerPacket != m_latestEchoedPacket) ? m_latestServerPacket : (ushort)0;
        m_latestEchoedPacket = m_latestServerPacket;
        EchoPacket echoPacket = new EchoPacket { packetNumber = echoNum, holdTime = Time.realtimeSinceStartupAsDouble - m_latestServerReciveTime };
        CommonPacket commonPacket = new CommonPacket { packetNumber = m_sentPackets, echoPacket = echoPacket };
        CompressedInputPacket inputPacket = new CompressedInputPacket(m_predictionInputHistory, m_predictionTimer.Tick, 0);

        commonPacket.Write(ref writer);
        inputPacket.Write(ref writer);
        
     

        m_sendTimeHistory[m_sentPackets%ECHO_BUFFER] = Time.realtimeSinceStartupAsDouble;
        m_driver.EndSend(writer);
        m_sentPackets++;
    }



    void addState(BallState state)
    {
        int diff = m_interpolationTimer.Tick - state.tick;
        if(diff <= -STATE_BUFFER/2)
        {
            Debug.LogWarning("fdfd");
            return;
        }

        if(diff > STATE_BUFFER / 2)
        {
            Debug.LogWarning("fdfd");
            return;
        }

        m_ballStateHistory[state.id * STATE_BUFFER + state.tick % STATE_BUFFER] = state;
    }

    NativeArray<RenderState> m_renderStates;


    private void Update()
    {


        processNetwork();

        var simulationStartTick = m_predictionTimer.Tick;
        var simulationTicks = m_predictionTimer.Update((float)Time.deltaTime);
        var renderTicks = m_interpolationTimer.Update((float)Time.deltaTime);

        if (renderTicks != 0)
        {
            ushort renderTick = m_interpolationTimer.Tick;

            for (int i = 0; i < MAX_CLIENTS; i++)
            {
                // 1. Get the minimum valid tick in the buffer window for this client
                ushort minTick = GetMinValidTick(m_ballStateHistory, i, renderTick, STATE_BUFFER);

                BallState from = default;
                BallState to = default;
             
      

                if (minTick != 0)
                {
                    int baseIndex = i * STATE_BUFFER;
                    to = m_ballStateHistory[baseIndex + (m_latestStatetick%STATE_BUFFER)];
                    from = m_ballStateHistory[baseIndex + (minTick % STATE_BUFFER)];

                    // 2. Loop from minTick to renderTick (Find closest past/present state)
                    int pastTicks = (short)(renderTick - minTick);
                    for (int k = 0; k < m_interpolationTimer.Tick; k++)
                    {
                      
                        var cur = m_ballStateHistory[baseIndex + (k % STATE_BUFFER)];

                        // Verify exact tick to reject stale slot data from old buffer cycles
                        if (cur.tick == k)
                        {
                            from = cur; // Keeps overwriting so 'from' lands on the newest valid tick <= renderTick
                            
                        }
                    }

                    // 3. Loop from renderTick + 1 to latest_tick (Find closest future state)
                

                    for (int k = (int)m_interpolationTimer.Tick; k <= m_latestStatetick; k++)
                    {
                    
                        var cur = m_ballStateHistory[baseIndex + (k % STATE_BUFFER)];

                        if (cur.tick == k)
                        {
                            to = cur; // First valid tick found is the immediate future state
                       
                            break;
                        }
                    }
                }

                // Fallbacks if one side is missing due to network jitter
   

                var copy = m_renderStates[i];
                copy.from = from;
                copy.to = to;
                m_renderStates[i] = copy;
            }
        }

        int id = 0;
        foreach (var bv in m_ballViews)
        {
            if (bv.gameObject.activeSelf)
            {
                var ex = m_renderStates[id];
                int d = (short)(ex.to.tick - ex.from.tick);

                float factor = 0f;
                if (d > 0)
                {
                    double total_time = d * m_interpolationTimer.SecondsPerTick;
                    double atime = m_interpolationTimer.TotalTime() - (ex.from.tick * m_interpolationTimer.SecondsPerTick);
                    factor = Mathf.Clamp01((float)(atime / total_time));
                }

                //bv.position = Vector3.Lerp(ex.from.position, ex.to.position, factor);
            }
            id++;
        }


        BallInput input = new BallInput();
        Vector2 z = m_ballDirection.ReadValue<Vector2>();
        input.direction = z.y * m_cam.GetFlatForward() + z.x * m_cam.GetFlatRight();
        input.jump = m_ballJump.IsPressed();

       // Debug.Log(simulationTicks);
        if (simulationTicks != 0 && simulationTicks != 1)
        {
            Debug.LogWarning("eeee");
        }
        for (var i = simulationStartTick; i < m_predictionTimer.Tick; i++)
        {
            input.tick = i;
            m_predictionInputHistory[i%INPUT_BUFFER] = input;
        }

        if (simulationTicks != 0)
        {
            if (m_serverConnection.IsCreated)
            {
                networkTick();
            }
        }
    }

   

}


