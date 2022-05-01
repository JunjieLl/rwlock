# FIFO可重入读写锁

## 1850668 李俊杰

## 简介

利用`AutoResetEvent`, ` ManualResetEvent` ,` Monitor.Enter`和`Monitor.Exit`实现了一个

<span style="color:green;">FIFO（公平）</span>的<span style="color:green;">可重入</span>读写锁。

## 数据结构

```c#
public class ReadWriteLockFIFO{
    private enum Kind{
        Reader,
        Writer
    }

    private struct QueueItem{
        public Kind kind;
        public int threadId;
        public QueueItem(Kind kind, int threadId){
            this.kind=kind;
            this.threadId=threadId;
        }
    }

    //mutex lock
    private readonly Object mutex = new Object();
    //for both reader and writer, wait
    private Queue<QueueItem> waitQueue = new Queue<QueueItem>();
    //ready to read, threadIds
    //<thread, count> support reEntrant
    private Dictionary<int,int> readyReader = new Dictionary<int, int>();
    //for reEntrant
    private int writeCount = 0;
    //record the writing Id to support reEntrant
    private int writingThreadId = -1;
    private AutoResetEvent writeEvent = new AutoResetEvent(false);
    private ManualResetEvent readEvent = new ManualResetEvent(false);
    private bool isDebug;
    public ReadWriteLockFIFO(bool isDebug){
        this.isDebug=isDebug;
    }

    public void getReadLock();

    public void releaseReadLock();

    public void getWriteLock();

    public void releaseWriteLock();
}
```

* `mutex`用于控制对类内相关变量的互斥访问
* `waitQueue`用于保存正在等待线程(`threadId`和读者/写者身份)
* `readyReader`用于保存正在读的读者(`threadId`和重入次数)，使用字典是为了记录重入数量以支持读者重入
* `writeCount`和`writingThreadId`的作用与`readyReader`一致，由于写者是互斥的所以一个变量就可以记录重入次数和`threadId`，而读者是共享的，所以需要集合来记录重入次数和`threadId`
* `writeEvent`和`readEvent`用于阻塞读者和写者。

## 读写锁工作流程

### 读锁

#### 获取读锁

```csharp
public void getReadLock(){
        Monitor.Enter(mutex);
        if(isDebug){
            Console.WriteLine($"[thread: {Thread.CurrentThread.ManagedThreadId} getReadStart]: waitQueue: {waitQueue.Count}, readyReader: {readyReader.Count}, writeCount: {writeCount}, writingThreadId: {writingThreadId}");
        }
        try{
            QueueItem item;
            //writing or waiting to write
            if(writeCount>0){
                //enqueue
                waitQueue.Enqueue(new QueueItem(Kind.Reader,Thread.CurrentThread.ManagedThreadId));
            }
            //reading
            else if(readyReader.Count>0){
                //reEntrant
                if(readyReader.ContainsKey(Thread.CurrentThread.ManagedThreadId)){
                    readyReader[Thread.CurrentThread.ManagedThreadId]+=1;
                }
                else{
                    //enqueue
                    waitQueue.Enqueue(new QueueItem(Kind.Reader,Thread.CurrentThread.ManagedThreadId));
                    item = waitQueue.Peek();
                    while(waitQueue.Count>0&&item.kind==Kind.Reader){
                        item = waitQueue.Dequeue();
                        if(readyReader.ContainsKey(item.threadId)){
                            readyReader[item.threadId]+=1;
                            throw new Exception("不应该出现");
                        }
                        else{
                            readyReader.Add(item.threadId,1);
                        }

                        if(waitQueue.Count>0){
                            item = waitQueue.Peek();
                        }
                    }
                }
            }
            //free
            else{
                //enqueue
                waitQueue.Enqueue(new QueueItem(Kind.Reader,Thread.CurrentThread.ManagedThreadId));
                item = waitQueue.Dequeue();
                //writer
                if(item.kind==Kind.Writer){
                    writingThreadId = item.threadId;
                    writeEvent.Set();
                    writeCount = 1;
                }
                //reader
                else{
                    readyReader.Add(item.threadId,1);
                    readEvent.Set();
                    while(waitQueue.Count>0){
                        item=waitQueue.Peek();
                        if(item.kind==Kind.Writer){
                            break;
                        }
                        waitQueue.Dequeue();
                        if(readyReader.ContainsKey(item.threadId)){
                            readyReader[item.threadId]+=1;
                            throw new Exception("不应该出现");
                        }
                        else{
                            readyReader.Add(item.threadId,1);
                        }
                    }
                }
            }
        }
        finally{
            Monitor.Exit(mutex);
        }
        if(isDebug){
            Console.WriteLine($"[thread: {Thread.CurrentThread.ManagedThreadId} getReadEnd]: waitQueue: {waitQueue.Count}, readyReader: {readyReader.Count}, writeCount: {writeCount}, writingThreadId: {writingThreadId}");
        }
        //wait read signal
        while(! readyReader.ContainsKey(Thread.CurrentThread.ManagedThreadId)){
            //yield
            Thread.Sleep(0);
        }
        readEvent.WaitOne();
    }
```

1. 如果写锁正在占用，则将该线程加入等待队列
2. 如果读锁正在占用
   1. 如果是重入线程(即该线程已经获取了读锁)，则增加字典中该线程的重入次数
   2. 如果不是重入线程，则将其加入等待队列，并给等待队列中第一个写请求前的读者分配读锁
3. 空闲，将该线程加入等待队列
   1. 如果等待队列头部是写请求，则该线程获得写锁
   2. 如果等待队列头部是读请求，则给等待队列中第一个写请求前的读者分配读锁

在readEvent.WaitOne前还有一个循环是为了确保只有在读者就绪队列中的读者能够进入，这里没有用到mutex进行互斥访问，因为读者就绪队列中对应于每个线程的item<threadId, reEntrantCount>只有该线程在获取锁时才能添加、释放锁时才能清除，换句话说每个线程对应的item其它线程都不会操作，所以不会出现不一致性。

#### 释放读锁

```c#
public void releaseReadLock(){
        Monitor.Enter(mutex);
        if(isDebug){
            Console.WriteLine($"[thread: {Thread.CurrentThread.ManagedThreadId} releaseReadStart]: waitQueue: {waitQueue.Count}, readyReader: {readyReader.Count}, writeCount: {writeCount}, writingThreadId: {writingThreadId}");
        }
        try{
            readyReader[Thread.CurrentThread.ManagedThreadId]-=1;
            if(readyReader[Thread.CurrentThread.ManagedThreadId]==0){
                readyReader.Remove(Thread.CurrentThread.ManagedThreadId);
            }
            
            if(readyReader.Count==0){
                readEvent.Reset();
            }
            //state transfer
            if(readyReader.Count==0&&waitQueue.Count>0){
                QueueItem item = waitQueue.Dequeue();
                //writer
                if(item.kind==Kind.Writer){
                    writingThreadId = item.threadId;
                    writeEvent.Set();
                    writeCount = 1;
                }
                //reader
                else{
                    readyReader.Add(item.threadId,1);
                    readEvent.Set();
                    while(waitQueue.Count>0){
                        item=waitQueue.Peek();
                        if(item.kind==Kind.Writer){
                            break;
                        }
                        waitQueue.Dequeue();
                        if(readyReader.ContainsKey(item.threadId)){
                            readyReader[item.threadId]+=1;
                            throw new Exception("不应该出现");
                        }
                        else{
                            readyReader.Add(item.threadId,1);
                        }
                    }
                }
            }
        }
        finally{
            Monitor.Exit(mutex);
        }
        if(isDebug){
            Console.WriteLine($"[thread: {Thread.CurrentThread.ManagedThreadId} releaseReadEnd]: waitQueue: {waitQueue.Count}, readyReader: {readyReader.Count}, writeCount: {writeCount}, writingThreadId: {writingThreadId}");
        }
    }
```

1. 每个读者线程将读者就绪队列中item的reEntrantCount减1，如果reEntrantCount等于0表示该线程结束对读锁的占用，将该线程从就绪队列中移除。
2. 如果读者就绪队列为空且等待队列不为空，则根据等待队列头部的线程（读/写）进行调度，调度流程与获取读者锁空闲时一致。

### 写锁

#### 获取写锁

```c#
public void getWriteLock(){
        Monitor.Enter(mutex);
        if(isDebug){
            Console.WriteLine($"[thread: {Thread.CurrentThread.ManagedThreadId} getWriteStart]: waitQueue: {waitQueue.Count}, readyReader: {readyReader.Count}, writeCount: {writeCount}, writingThreadId: {writingThreadId}");
        }
        try{
            //reading
            if(readyReader.Count>0){
                //enqueue
                waitQueue.Enqueue(new QueueItem(Kind.Writer,Thread.CurrentThread.ManagedThreadId));
                QueueItem item = waitQueue.Peek();
                while(waitQueue.Count>0&&item.kind==Kind.Reader){
                    if(readyReader.ContainsKey(item.threadId)){
                        readyReader[item.threadId]+=1;
                        throw new Exception("不应该出现");
                    }
                    else{
                        readyReader.Add(item.threadId,1);
                    }
                    waitQueue.Dequeue();
                    if(waitQueue.Count>0){
                        item=waitQueue.Peek();
                    }
                }
            }
            //writing
            else if(writeCount>0){
                //reEntrant
                if(writingThreadId==Thread.CurrentThread.ManagedThreadId){
                    writeCount+=1;
                    writeEvent.Set();
                }
                else{
                    waitQueue.Enqueue(new QueueItem(Kind.Writer,Thread.CurrentThread.ManagedThreadId));
                }
            }
            //free
            else{
                waitQueue.Enqueue(new QueueItem(Kind.Writer,Thread.CurrentThread.ManagedThreadId));
                QueueItem item = waitQueue.Dequeue();
                //writer
                if(item.kind==Kind.Writer){
                    writingThreadId = item.threadId;
                    writeEvent.Set();
                    writeCount = 1;
                }
                //reader
                else{
                    readyReader.Add(item.threadId,1);
                    readEvent.Set();
                    while(waitQueue.Count>0){
                        item=waitQueue.Peek();
                        if(item.kind==Kind.Writer){
                            break;
                        }
                        waitQueue.Dequeue();
                        if(readyReader.ContainsKey(item.threadId)){
                            readyReader[item.threadId]+=1;
                            throw new Exception("不应该出现");
                        }
                        else{
                            readyReader.Add(item.threadId,1);
                        }
                    }
                }
            }
        }
        finally{
            Monitor.Exit(mutex);
        }
        if(isDebug){
            Console.WriteLine($"[thread: {Thread.CurrentThread.ManagedThreadId} getWriteEnd]: waitQueue: {waitQueue.Count}, readyReader: {readyReader.Count}, writeCount: {writeCount}, writingThreadId: {writingThreadId}");
        }
        //prevent other write thread from entering
        //keep exclusive in reEntrant
        while(writingThreadId!=Thread.CurrentThread.ManagedThreadId){
            Thread.Sleep(0);
        }
        writeEvent.WaitOne();
    }
```

1. 如果读锁正在被占用，则将该线程加入等待队列，并给等待队列中第一个写请求前的读请求分配读锁
2. 如果写锁正在被占用
   1. 如果是当前线程在占用（重入），则增加重入次数(writeCount)
   2. 如果不是当前线程占用，则将该线程加入等待队列
3. 空闲时调度流程与获取读锁时一致。

与获取读锁一致，也有一个循环用于控制写者的进入，由于写锁是互斥的，并且对于writingThreadId的修改只会在写线程运行前和释放时修改，中途不会有修改，所以也不会造成不一致性。

#### 释放写锁

```csharp
public void releaseWriteLock(){
        Monitor.Enter(mutex);
        if(isDebug){
            Console.WriteLine($"[thread: {Thread.CurrentThread.ManagedThreadId} releaseWriteStart]: waitQueue: {waitQueue.Count}, readyReader: {readyReader.Count}, writeCount: {writeCount}, writingThreadId: {writingThreadId}");
        }
        try{
            writeCount-=1;
            if(writeCount==0){
                writingThreadId=-1;
            }

            if(writeCount==0&&waitQueue.Count>0){
                QueueItem item = waitQueue.Dequeue();
                //writer
                if(item.kind==Kind.Writer){
                    writingThreadId = item.threadId;
                    writeEvent.Set();
                    writeCount = 1;
                }
                //reader
                else{
                    readyReader.Add(item.threadId,1);
                    readEvent.Set();
                    while(waitQueue.Count>0){
                        item=waitQueue.Peek();
                        if(item.kind==Kind.Writer){
                            break;
                        }
                        waitQueue.Dequeue();
                        if(readyReader.ContainsKey(item.threadId)){
                            readyReader[item.threadId]+=1;
                            throw new Exception("不应该出现");
                        }
                        else{
                            readyReader.Add(item.threadId,1);
                        }
                    }
                }
            }
        }
        finally{
            Monitor.Exit(mutex);
        }
        if(isDebug){
            Console.WriteLine($"[thread: {Thread.CurrentThread.ManagedThreadId} releaseWriteEnd]: waitQueue: {waitQueue.Count}, readyReader: {readyReader.Count}, writeCount: {writeCount}, writingThreadId: {writingThreadId}");
        }
    }
```

1. 减少重入次数（writeCount)，如果重入次数为0，表示该线程结束对写锁的占用，将writingThreadId置为-1
2. 如果重入次数为0且等待队列不为空，则根据等待队列头部的线程（读/写）进行调度，调度流程与获取读者锁空闲时一致。



## 测试案例

### 测试用例1

测试高并发下是否会死锁，并发量为1400000。

模拟大规模的读和写操作。

```c#
class Test{
    private static ReadWriteLockFIFO readWriteLock = new ReadWriteLockFIFO(true);
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
            if(i%13000==0){
                tasks.Add(Task.Run(()=>write(new Random().Next()%200)));
            }
            else{
                tasks.Add(Task.Run(()=>read()));
            }
        }
        Task.WaitAll(tasks.ToArray());
    }
}
```

输出的最后一部分

```
...
read res: 10821
[thread: 3 releaseReadStart]: waitQueue: 0, readyReader: 6, writeCount: 0, writingThreadId: -1
[thread: 3 releaseReadEnd]: waitQueue: 0, readyReader: 5, writeCount: 0, writingThreadId: -1
[thread: 21 releaseReadStart]: waitQueue: 0, readyReader: 5, writeCount: 0, writingThreadId: -1
[thread: 21 releaseReadEnd]: waitQueue: 0, readyReader: 4, writeCount: 0, writingThreadId: -1
[thread: 27 getReadStart]: waitQueue: 0, readyReader: 4, writeCount: 0, writingThreadId: -1
[thread: 27 getReadEnd]: waitQueue: 0, readyReader: 5, writeCount: 0, writingThreadId: -1
read res: 10821
[thread: 22 getReadStart]: waitQueue: 0, readyReader: 5, writeCount: 0, writingThreadId: -1
[thread: 22 getReadEnd]: waitQueue: 0, readyReader: 6, writeCount: 0, writingThreadId: -1
[thread: 27 releaseReadStart]: waitQueue: 0, readyReader: 6, writeCount: 0, writingThreadId: -1
read res: 10821
[thread: 22 releaseReadStart]: waitQueue: 0, readyReader: 5, writeCount: 0, writingThreadId: -1
[thread: 27 releaseReadEnd]: waitQueue: 0, readyReader: 5, writeCount: 0, writingThreadId: -1
[thread: 22 releaseReadEnd]: waitQueue: 0, readyReader: 4, writeCount: 0, writingThreadId: -1
[thread: 6 releaseReadStart]: waitQueue: 0, readyReader: 4, writeCount: 0, writingThreadId: -1
[thread: 6 releaseReadEnd]: waitQueue: 0, readyReader: 3, writeCount: 0, writingThreadId: -1
[thread: 17 getReadStart]: waitQueue: 0, readyReader: 3, writeCount: 0, writingThreadId: -1
[thread: 17 getReadEnd]: waitQueue: 0, readyReader: 4, writeCount: 0, writingThreadId: -1
read res: 10821
[thread: 34 releaseReadStart]: waitQueue: 0, readyReader: 4, writeCount: 0, writingThreadId: -1
[thread: 34 releaseReadEnd]: waitQueue: 0, readyReader: 3, writeCount: 0, writingThreadId: -1
[thread: 17 releaseReadStart]: waitQueue: 0, readyReader: 3, writeCount: 0, writingThreadId: -1
[thread: 17 releaseReadEnd]: waitQueue: 0, readyReader: 2, writeCount: 0, writingThreadId: -1
[thread: 30 releaseReadStart]: waitQueue: 0, readyReader: 2, writeCount: 0, writingThreadId: -1
[thread: 30 releaseReadEnd]: waitQueue: 0, readyReader: 1, writeCount: 0, writingThreadId: -1
[thread: 32 releaseReadStart]: waitQueue: 0, readyReader: 1, writeCount: 0, writingThreadId: -1
[thread: 32 releaseReadEnd]: waitQueue: 0, readyReader: 0, writeCount: 0, writingThreadId: -1
```

最后一行输出后程序终止，且等待队列为空，读者就绪队列为空，写锁也没有被占用。

### 测试用例2

测试案例来自.Net官方文档[ReadWriteLockSlim](https://docs.microsoft.com/zh-cn/dotnet/api/system.threading.readerwriterlockslim?view=net-6.0)，期中删去了AddOrUpdateStatus以及AddWithTimeout函数，其首先启动写者写入，然后启动两个读者分别从两端开始读 。

测试结果如下：

```
Task 1 read 0 items: 

Task 2 read 0 items: 

Task 3 wrote 17 items

Task 2 read 17 items: [lima beans] [raddichio] [cucumber] [radish] [corn] [lime leaves] [grape leaves] [spinach] [plantain] [cabbage] [brussel sprout] [beet] [baby turnip] [sorrel] [carrot] [cauliflower] [broccoli] 

Task 1 read 17 items: [broccoli] [cauliflower] [carrot] [sorrel] [baby turnip] [beet] [brussel sprout] [cabbage] [plantain] [spinach] [grape leaves] [lime leaves] [corn] [radish] [cucumber] [raddichio] [lima beans] 


Values in synchronized cache: 
   1: broccoli
   2: cauliflower
   3: carrot
   4: sorrel
   5: baby turnip
   6: beet
   7: brussel sprout
   8: cabbage
   9: plantain
   10: spinach
   11: grape leaves
   12: lime leaves
   13: corn
   14: radish
   15: cucumber
   16: raddichio
   17: lima beans
```

### 测试用例3

测试读写锁的可重入性。

```c#
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
```

输出结果如下，读锁可重入和写锁可重入。

```
[thread: 4 getWriteStart]: waitQueue: 0, readyReader: 0, writeCount: 0, writingThreadId: -1
[thread: 4 getWriteEnd]: waitQueue: 0, readyReader: 0, writeCount: 1, writingThreadId: 4
[thread: 6 getReadStart]: waitQueue: 0, readyReader: 0, writeCount: 1, writingThreadId: 4
[thread: 6 getReadEnd]: waitQueue: 1, readyReader: 0, writeCount: 1, writingThreadId: 4
[thread: 4 getWriteStart]: waitQueue: 1, readyReader: 0, writeCount: 1, writingThreadId: 4
[thread: 4 getWriteEnd]: waitQueue: 1, readyReader: 0, writeCount: 2, writingThreadId: 4
ReEntrant Write Success
[thread: 4 releaseWriteStart]: waitQueue: 1, readyReader: 0, writeCount: 2, writingThreadId: 4
[thread: 4 releaseWriteEnd]: waitQueue: 1, readyReader: 0, writeCount: 1, writingThreadId: 4
[thread: 4 releaseWriteStart]: waitQueue: 1, readyReader: 0, writeCount: 1, writingThreadId: 4
[thread: 4 releaseWriteEnd]: waitQueue: 0, readyReader: 1, writeCount: 0, writingThreadId: -1
[thread: 6 getReadStart]: waitQueue: 0, readyReader: 1, writeCount: 0, writingThreadId: -1
[thread: 6 getReadEnd]: waitQueue: 0, readyReader: 1, writeCount: 0, writingThreadId: -1
ReEntrant Read Success
[thread: 6 releaseReadStart]: waitQueue: 0, readyReader: 1, writeCount: 0, writingThreadId: -1
[thread: 6 releaseReadEnd]: waitQueue: 0, readyReader: 1, writeCount: 0, writingThreadId: -1
[thread: 6 releaseReadStart]: waitQueue: 0, readyReader: 1, writeCount: 0, writingThreadId: -1
[thread: 6 releaseReadEnd]: waitQueue: 0, readyReader: 0, writeCount: 0, writingThreadId: -1
```

### 测试用例4

利用测试用例1，与标准库中的ReadWriteLockSlim进行对比，并发量同样为1400000。

结果如下：

```
mine: 26404.8428ms, baseLine: 36627.4825ms
```

快了很多！

## 分析

实现了FIFO的公平读写锁不会有饥饿现象，能够支持并发读取，能够支持并发写请求，支持同一线程读锁重入读锁，写锁重入写锁，在锁阻塞时使用Thread.Sleep(0)主动释放对处理器的占用。

缺点是不支持可升级锁以及写锁和读锁之间的互相重入。

