
using System;
using System.Threading;

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
}