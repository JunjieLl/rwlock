using System;
class Test3{
    private static ReadWriteLockFIFO readWriteLock = new ReadWriteLockFIFO(true);
    static void reEntrantRead(){
        readWriteLock.getReadLock();
        readWriteLock.getReadLock();
        Console.WriteLine("ReEntrant Read Success");
        readWriteLock.releaseReadLock();
        readWriteLock.releaseReadLock();
    }

    static void reEntrantWrite(){
        readWriteLock.getWriteLock();
        readWriteLock.getWriteLock();
        Console.WriteLine("ReEntrant Write Success");
        readWriteLock.releaseWriteLock();
        readWriteLock.releaseWriteLock();
    }


    static void Main(){
        Task task1 = Task.Run(()=>reEntrantWrite());
      	Task task2 = Task.Run(()=>reEntrantRead());
        Task.WaitAll(task1,task2);
    }
}