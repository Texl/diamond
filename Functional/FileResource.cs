using System;
using System.Collections.Generic;
using System.IO;
using static PlayStudios.Functional.Alg;


namespace PlayStudios.Functional
{
#if false
    public static class FileResource
    {

        // Kind of 'meh' - may or may not factor this otu.
        private sealed class DeathWatch
        {
            public DeathWatch(Action onDeath)
            {
                mOnDeath = onDeath;
            }
            private readonly Action mOnDeath;
            ~DeathWatch()
            {
                mOnDeath();
            }
        }


        private sealed class SharePack
        {
            public SharePack(string originalPath)
            {
                MutablePath = originalPath;
            }
            public readonly object mTheLock = new string[] { "" };
            public string MutableIdentity = System.Guid.NewGuid().ToString();
            public string MutablePath;

        }


        private sealed class GetIdentityAndDrawObject<T> // to explicitly control the scope, to capture 'this' etc.
        {
            public GetIdentityAndDrawObject(DeathWatch deathWatch, SharePack sharePack, Func<Stream, T> stripFile)
            {
                mDeathWatch = deathWatch;
                SharePack = sharePack;
                mStripFile = stripFile;
            }

            public OfResourceBank.IdentityAndDraw<T> GetIdentityAndDraw() // key is that this captures 'this' and the DeathWatch item.
            {
                lock (SharePack.mTheLock)
                {
                    return
                        new OfResourceBank.IdentityAndDraw<T>(
                            SharePack.MutableIdentity,
                            () =>
                            {
                                GC.KeepAlive(mDeathWatch); // just to highlight, this is the rationale for having the object itself.
                                lock (SharePack.mTheLock)
                                {
                                    /* Thought this could be amusing; but problem is hanging around here locks the (current) R.Node GetValueHereAssumingAlreadyLocked/Recreate() which would be holding it and blocking replacement
                                    if (UnderReplacement)
                                    {
                                        System.Threading.Monitor.Wait(mTheLock);
                                    }
                                    */
                                    int retryCount = 10;
                                    for (;;) //  (int i = 0; i < 10; ++ i) // gets some stutter on editing a file
                                    {
                                        Stream stream = null;
                                        try
                                        {
                                            stream = File.OpenRead(SharePack.MutablePath);

                                        }
                                        catch (IOException)
                                        {
                                            if (retryCount == 0)
                                            {
                                                throw;
                                            }
                                            System.Threading.Thread.Sleep(100);
                                            -- retryCount;
                                            continue;
                                        }
                                        using (stream)
                                        {
                                            return mStripFile(stream);
                                        }

                                    }
                                }
                            });
                }
            }

            public readonly SharePack SharePack;
            private readonly Func<Stream, T> mStripFile;
            private readonly DeathWatch mDeathWatch; // keeping it alive by anyone capturing 'this'

        }
        
        public static Action<Action<Action>> WithFileSystemWatcher(Action<Option<string>> thereWasAChangeToSetPath, string originalPath)
        {
            var watcher = new FileSystemWatcher(Path.GetDirectoryName(originalPath), Path.GetFileName(originalPath));
            Action thereWasAChange = () => thereWasAChangeToSetPath(None<string>());
            watcher.Changed += new FileSystemEventHandler((source, e) => thereWasAChange());
            watcher.Created += new FileSystemEventHandler((source, e) => thereWasAChange());
            watcher.Deleted += new FileSystemEventHandler((source, e) => thereWasAChange()); // if it was deleted, the subsequent reload will grenade but the caller will still kill his old
            watcher.Renamed += new RenamedEventHandler((source, e) => thereWasAChangeToSetPath(Some(e.FullPath)));
            return
                runWithWatcherDispose =>
                {
                    runWithWatcherDispose(watcher.Dispose);
                    watcher.EnableRaisingEvents = true;
                };
        }

        /// <summary>
        ///
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="thereWasAChangeAndPossibleNewPath"></param>
        /// <param name="originalPath"></param>
        /// <returns>Run-with, with the inner action being the dispose</returns>
        public delegate Action<Action<Action>> DWithFileWatcherToDispose(Action<Option<string>> thereWasAChangeAndPossibleNewPath, string originalPath);

        public static OfResourceBank.DResourceSourceDefinition<T> GetFileResource<T>(string originalPath, Func<Stream, T> stripFile) =>
            GetFileResource(originalPath, stripFile, (_, __) => new Action<Action<Action>>(rw => rw(() => { })));



        public static OfResourceBank.DResourceSourceDefinition<T> GetFileResource<T>(string originalPath, Func<Stream, T> stripFile, DWithFileWatcherToDispose withFileWatcherToDispose)
        {

            // weakref - don't hard link the recipient - he goes we don't have it
            // should only ever get 1 element
            var listenersForChange = new List<WeakReference<Action<Func<OfResourceBank.IdentityAndDraw<T>>>>>();

            var sharePack = new SharePack(originalPath);
            //var getIdentityAndDrawObject = new GetIdentityAndDrawObject<T>(new DeathWatch(watcher.Dispose), originalPath, stripFile);

            var theLock = sharePack.mTheLock;

            WeakReference<GetIdentityAndDrawObject<T>> weakGetIdentity = null;

            Action<Option<string>> thereWasAChangeToSetPath =
                possibleSuccessorPath =>
                {
                    WeakReference<Action<Func<OfResourceBank.IdentityAndDraw<T>>>>[] relevantListenersForChange = null;
                    lock (theLock)
                    {
                        possibleSuccessorPath.ForEach(successorPath => sharePack.MutablePath = successorPath);
                        sharePack.MutableIdentity = Guid.NewGuid().ToString();
                        relevantListenersForChange = listenersForChange.ToArray();
                        //getIdentityAndDrawObject.UnderReplacement = true;
                    }

                    // Never, ever call back in lock we are the downsteram lock
                    foreach (var listener in relevantListenersForChange)
                    {
                        Action<Func<OfResourceBank.IdentityAndDraw<T>>> func = null;
                        if (listener.TryGetTarget(out func))
                        {
                            GetIdentityAndDrawObject<T> getIdentity = null;
                            if (weakGetIdentity.TryGetTarget(out getIdentity))
                            {
                                func(getIdentity.GetIdentityAndDraw);
                            }
                        }
                    }

                    lock (theLock)
                    {
                        // getIdentityAndDrawObject.UnderReplacement = false;
                        System.Threading.Monitor.PulseAll(theLock);  // wake up any waiting get operation
                    }

                    // don't capture watcher indirectly in here, or it'll be a circular and never dispose.
                };


            return
                With(
                    withFileWatcherToDispose(
                        thereWasAChangeToSetPath,
                        originalPath),
                    watcherDispose =>
                    {
                        var getIdentityAndDrawObject = new GetIdentityAndDrawObject<T>(new DeathWatch(watcherDispose), sharePack, stripFile);
                        weakGetIdentity = new WeakReference<GetIdentityAndDrawObject<T>>(getIdentityAndDrawObject);

                        return new OfResourceBank.DResourceSourceDefinition<T>(
                            sinkReplacementOnChange => // the guy accepting the sink.
                            {
                                lock (theLock)
                                {
                                    listenersForChange.Add(new WeakReference<Action<Func<OfResourceBank.IdentityAndDraw<T>>>>(sinkReplacementOnChange));
                                    return getIdentityAndDrawObject.GetIdentityAndDraw; // deliberately capturing the deathwatch in its contents - anyone keeping the ability to read keeps the watcher alive
                                }
                            });
                    });

        }


    }
#endif
}
