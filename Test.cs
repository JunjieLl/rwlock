
class Test{
    private static ReadWriteLockFIFO readWriteLock = new ReadWriteLockFIFO(false);
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

    static void Main(string[] args){
        List<Task> tasks = new List<Task>();
        for(int i=0;i<1400000;++i){
            if(i%1300==0){
                tasks.Add(Task.Run(()=>write(new Random().Next()%200)));
            }
            else{
                tasks.Add(Task.Run(()=>read()));
            }
        }
        Task.WaitAll(tasks.ToArray());
    }
}