using System;
using System.Diagnostics;
class Test{
    private static ReadWriteLockFIFO readWriteLock = new ReadWriteLockFIFO(false);
    private static ReaderWriterLockSlim baseLine = new ReaderWriterLockSlim();
    private static int res = 0;
    
    static void read(){
        readWriteLock.getReadLock();
        Console.WriteLine($"read res: {res}");
        readWriteLock.releaseReadLock();
    }
    
    static void write(int i){
        readWriteLock.getWriteLock();
        Console.WriteLine($"before write res: {res}");
        res += i;
        Console.WriteLine($"after write res: {res}");
        readWriteLock.releaseWriteLock();
    }

    static void readBaseLine(){
        baseLine.EnterReadLock();
        Console.WriteLine($"read res: {res}");
        baseLine.ExitReadLock();
    }
    
    static void writeBaseLine(int i){
        baseLine.EnterWriteLock();
        Console.WriteLine($"before write res: {res}");
        res += i;
        Console.WriteLine($"after write res: {res}");
        baseLine.ExitWriteLock();
    }

    static void Main(string[] args){
        Stopwatch stopwatch = new Stopwatch();

        List<Task> tasks = new List<Task>();
        stopwatch.Start();
        for(int i=0;i<1400000;++i){
            if(i%13000==0){
                tasks.Add(Task.Run(()=>write(new Random().Next()%200)));
            }
            else{
                tasks.Add(Task.Run(()=>read()));
            }
        }
        Task.WaitAll(tasks.ToArray());
        stopwatch.Stop();
        var firstSeconds = stopwatch.Elapsed.TotalMilliseconds;

        //baseLine
        stopwatch.Reset();
        tasks.Clear();
        stopwatch.Start();
        for(int i=0;i<1400000;++i){
            if(i%13000==0){
                tasks.Add(Task.Run(()=>writeBaseLine(new Random().Next()%200)));
            }
            else{
                tasks.Add(Task.Run(()=>readBaseLine()));
            }
        }
        Task.WaitAll(tasks.ToArray());
        stopwatch.Stop();
        var secondSeconds = stopwatch.Elapsed.TotalMilliseconds;
        Console.WriteLine($"mine: {firstSeconds}ms, baseLine: {secondSeconds}ms");
    }
}