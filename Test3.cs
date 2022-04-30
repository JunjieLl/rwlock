
class Test3{
    private static ReadWriteLock readWriteLock = new ReadWriteLock(true);
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
        Task task = Task.Run(()=>reEntrantWrite());
        Task.WaitAll(task);
    }
}