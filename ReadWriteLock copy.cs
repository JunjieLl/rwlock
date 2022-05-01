
using System;
using System.Threading;

public class ReadWriteLockRe{
    //mutex lock for writeCount, readCount, writeCount, writeWaitQueue.Count
    private readonly Object mutex = new Object();
    private int readCount = 0;
    private int readWait = 0;
    private int readReady = 0;
    private Queue<int> writeWaitQueue = new Queue<int>();
    private int writeCount = 0;
    private int writingThreadId = -1;//record the writing Id to support reEntrant

    private AutoResetEvent writeEvent = new AutoResetEvent(false);
    private ManualResetEvent readEvent = new ManualResetEvent(false);

    private bool isDebug;

    public ReadWriteLockRe(bool isDebug){
        this.isDebug=isDebug;
    }

    public void getReadLock(){
        Monitor.Enter(mutex);
        if(isDebug){
            Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId}, [getReadBlock] start: readCount: {readCount}, readWait:{readWait}, writeCount: {writeCount}, writeWait:{writeWaitQueue.Count}, writingThread: {writingThreadId}");
        }
        try{
            //writing or write request
            if(writeCount>0||writeWaitQueue.Count>0){
                readEvent.Reset();
                readWait+=1;
            }
            else{
                //free
                if(readCount==0){
                    readEvent.Set();
                }
                //reading--no action
                readReady+=1+readWait;
                readWait=0;
            }
        }
        finally{
            if(isDebug){
                Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId}, [getReadBlock] end: readCount: {readCount}, readWait:{readWait}, writeCount: {writeCount}, writeWaitQueue.Count:{writeWaitQueue.Count}, writingThread: {writingThreadId}");
            }
            Monitor.Exit(mutex);
        }
        //wait read signal
        
        readEvent.WaitOne();

        Monitor.Enter(mutex);
        try{
            readCount+=1;
            if(readCount==readReady){
                //本次读者已经放完
                readEvent.Reset();
                readReady=0;
            }
        }
        finally{
            Monitor.Exit(mutex);
        }
    }

    public void releaseReadLock(){
        Monitor.Enter(mutex);
        if(isDebug){
            Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId}, [releaseReadBlock] start: readCount: {readCount}, readWait:{readWait}, writeCount: {writeCount}, writeWait:{writeWaitQueue.Count}, writingThread: {writingThreadId}");
        }
        try{
            readCount-=1;
            if(readCount<0){
                throw new Exception("readCount < 0");
            }
            if(readCount==0){
                readEvent.Reset();
                if(writeWaitQueue.Count>0){
                    writeCount=1;
                    writingThreadId=writeWaitQueue.Dequeue();
                    writeEvent.Set();
                }
            }
        }
        finally{
            if(isDebug){
                Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId}, [releaseReadBlock] end: readCount: {readCount}, readWait:{readWait}, writeCount: {writeCount}, writeWait:{writeWaitQueue.Count}, writingThread: {writingThreadId}");
            }
            Monitor.Exit(mutex);
        }
    }

    public void getWriteLock(){
        Monitor.Enter(mutex);
        if(isDebug){
            Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId}, [getWriteBlock] start: readCount: {readCount}, readWait:{readWait}, writeCount: {writeCount}, writeWait:{writeWaitQueue.Count}, writingThread: {writingThreadId}");
        }
        try{
            //reading
            if(readCount>0){
                writeWaitQueue.Enqueue(Thread.CurrentThread.ManagedThreadId);
            }
            else{
                //writing
                if(writeCount>0){
                    if(writingThreadId==Thread.CurrentThread.ManagedThreadId){
                        writeCount+=1;
                        writeEvent.Set();//reentrant
                    }
                    else{
                        writeWaitQueue.Enqueue(Thread.CurrentThread.ManagedThreadId);
                    }
                }
                else{
                    writeCount=1;
                    writingThreadId = Thread.CurrentThread.ManagedThreadId;
                    writeEvent.Set();
                }
            }
        }
        finally{
            if(isDebug){
                Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId}, [getWriteBlock] end: readCount: {readCount}, readWait:{readWait}, writeCount: {writeCount}, writeWait:{writeWaitQueue.Count}, writingThread: {writingThreadId}");
            }
            Monitor.Exit(mutex);
        }
        //prevent other write thread from entering
        //keep exclusive in reEntrant
        //spin wait
        while(writingThreadId!=Thread.CurrentThread.ManagedThreadId){
            Thread.Sleep(1);
        }
        writeEvent.WaitOne();
    }

    public void releaseWriteLock(){
        Monitor.Enter(mutex);
        if(isDebug){
            Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId}, [releaseWriteBlock] start: readCount: {readCount}, readWait:{readWait}, writeCount: {writeCount}, writeWait:{writeWaitQueue.Count}, writingThread: {writingThreadId}");
        }
        try{
            //wait request
            writeCount-=1;
            if(writeCount==0){
                writingThreadId=-1;

                if(writeWaitQueue.Count>0){
                    writeCount=1;
                    writingThreadId=writeWaitQueue.Dequeue();
                    writeEvent.Set();
                }
                else{
                    if(readWait>0){
                        readCount+=readWait;
                        readWait=0;
                        readEvent.Set();
                    }
                }
            }
        }
        finally{
            if(isDebug){
                Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId}, [releaseWriteBlock] end: readCount: {readCount}, readWait:{readWait}, writeCount: {writeCount}, writeWait:{writeWaitQueue.Count}, writingThread: {writingThreadId}");
            }
            Monitor.Exit(mutex);
        }
    }
}