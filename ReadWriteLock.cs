
using System;
using System.Threading;

public class ReadWriteLock{
    //mutex lock for writeCount, readCount, isWriting, writeWait
    private Object mutex = new Object();
    private int readCount = 0;
    private int readWait = 0;
    private bool isWriting = false;
    private int writeWait = 0;

    private AutoResetEvent writeEvent = new AutoResetEvent(false);
    private ManualResetEvent readEvent = new ManualResetEvent(false);

    private bool isDebug;

    public ReadWriteLock(bool isDebug){
        this.isDebug=isDebug;
    }

    public void getReadLock(){
        Monitor.Enter(mutex);
        if(isDebug){
            Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId}, [getReadBlock] start: readCount: {readCount}, readWait:{readWait}, isWriting: {isWriting}, writeWait:{writeWait}");
        }
        try{
            //writing or write request
            if(isWriting||writeWait>0){
                readEvent.Reset();
                readWait+=1;
            }
            else{
                //free
                if(readCount==0){
                    readEvent.Set();
                }
                //reading--no action
                readCount+=1+readWait;
                readWait=0;
            }
        }
        finally{
            if(isDebug){
                Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId}, [getReadBlock] end: readCount: {readCount}, readWait:{readWait}, isWriting: {isWriting}, writeWait:{writeWait}");
            }
            Monitor.Exit(mutex);
        }
        //wait read signal
        
        readEvent.WaitOne();
        
    }

    public void releaseReadLock(){
        Monitor.Enter(mutex);
        if(isDebug){
            Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId}, [releaseReadBlock] start: readCount: {readCount}, readWait:{readWait}, isWriting: {isWriting}, writeWait:{writeWait}");
        }
        try{
            readCount-=1;
            if(readCount<0){
                throw new Exception("readCount < 0");
            }
            if(readCount==0){
                readEvent.Reset();
                if(writeWait>0){
                    isWriting=true;
                    writeWait-=1;
                    writeEvent.Set();
                }
            }
        }
        finally{
            if(isDebug){
                Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId}, [releaseReadBlock] end: readCount: {readCount}, readWait:{readWait}, isWriting: {isWriting}, writeWait:{writeWait}");
            }
            Monitor.Exit(mutex);
        }
    }

    public void getWriteLock(){
        Monitor.Enter(mutex);
        if(isDebug){
            Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId}, [getWriteBlock] start: readCount: {readCount}, readWait:{readWait}, isWriting: {isWriting}, writeWait:{writeWait}");
        }
        try{
            //reading
            if(readCount>0){
                writeWait += 1;
            }
            else{
                //writing
                if(isWriting){
                    writeWait+=1;
                }
                else{
                    isWriting=true;
                    writeEvent.Set();
                }
            }
        }
        finally{
            if(isDebug){
                Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId}, [getWriteBlock] end: readCount: {readCount}, readWait:{readWait}, isWriting: {isWriting}, writeWait:{writeWait}");
            }
            Monitor.Exit(mutex);
        }
        
        writeEvent.WaitOne();
        
    }

    public void releaseWriteLock(){
        Monitor.Enter(mutex);
        if(isDebug){
            Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId}, [releaseWriteBlock] start: readCount: {readCount}, readWait:{readWait}, isWriting: {isWriting}, writeWait:{writeWait}");
        }
        try{
            //wait request
            if(writeWait>0){
                writeWait-=1;
                writeEvent.Set();
            }
            else{
                isWriting=false;
                if(readWait>0){
                    readCount+=readWait;
                    readWait=0;
                    readEvent.Set();
                }
            }
        }
        finally{
            if(isDebug){
                Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId}, [releaseWriteBlock] end: readCount: {readCount}, readWait:{readWait}, isWriting: {isWriting}, writeWait:{writeWait}");
            }
            Monitor.Exit(mutex);
        }
    }
}