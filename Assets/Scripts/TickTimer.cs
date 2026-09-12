public struct TickTimer
{
    public ushort Tick;
    public double Acc;
    public double SecondsPerTick;

    public int Update(double delta)
    {
        Acc += delta;

        int ticks = 0;
        while (Acc > SecondsPerTick)
        {
            ticks++;
            Acc -= SecondsPerTick;
        }
        return ticks;
    }
}
