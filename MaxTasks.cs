using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Spi
{
    public class MaxTasks
    {
        ManualResetEvent _finished;
        IEnumerator<Task> _taskEnum;

        long _counter;
        long _error;
        long _done;
        /// <summary>
        /// the idea is the following:
        /// 
        /// 1, start MAX tasks
        /// 2, WHEN a task come to his end, 
        ///     pull out a new taks from the enumerator and start this new taks
        ///     
        /// so only when a task has ended a new one is started.
        /// That should be the limiting logic here.
        /// 
        /// ad "_counter=1" (by Mike Brüstle. Thanks! :-) )
        ///     It's the "outer bracket".
        ///     This bracket is "closed" at "if (Interlocked.Decrement(ref _counter) != 0)"
        ///     
        ///     when we start with _counter=0 following could happen:
        ///     1, we begin starting tasks
        ///     2, this task increments _counter to 1
        ///     3, nothing else is happening 
        ///     4, next this task ends an decrements _counter to zero 
        ///         AND checking it for zero! ==> indicating "we are all done"
        ///         But there was only ONE task started and one ended. Not good.
        ///         
        ///     With _counter set to 1 we make sure that
        ///     1,      the "main loop" could be last last to decrement to zero
        ///     2, OR   the decrement in the postWork-tasks
        ///
        /// scenario #1
        ///     when all the tasks are finished BEFORE the main loop enters the if,
        ///     the _counter is still sitting at 1.
        ///     So the last decrement will give 0 and we exit the function
        /// 
        /// scenario #2
        ///     the main-starting-loop exists BEFORE the last working task,
        ///     then one of the working tasks will do the last decrement to 0
        ///     setting the ManualResetEvent _finish and the main-function exits.
        /// 
        /// </summary>
        /// <param name="tasks"></param>
        /// <param name="MaxParallel"></param>
        public void Run(IEnumerable<Task> tasks, int MaxParallel)
        {
            using (_taskEnum = tasks.GetEnumerator())
            using (_finished = new ManualResetEvent(false))
            {
                _counter = 1; // !!!!! Mike's way :-)               //  ---+
                                                                    //     |
                for (int i = 0; i < MaxParallel; ++i)               //     |
                {                                                   //     |
                    if (StartNextTask() == false)                   //     |
                    {                                               //     |
                        break;                                      //     |
                    }                                               //     |
                }                                                   //     |
                                                                    //     |
                if (Interlocked.Decrement(ref _counter) != 0)       //  <--+
                {
                    _finished.WaitOne();
                }
            }
        }
        public Task Start(IEnumerable<Task> tasks, int MaxParallel)
        {
            _taskEnum = tasks.GetEnumerator();
            _finished = new ManualResetEvent(false);

            {
                _counter = 1; // !!!!! Mike's way :-)

                for (int i = 0; i < MaxParallel; ++i)
                {
                    if (StartNextTask() == false)
                    {
                        break;
                    }
                }

                return
                    Task.Run(() =>
                    {
                       if (Interlocked.Decrement(ref _counter) != 0)
                       {
                           _finished.WaitOne();
                       }

                        _taskEnum.Dispose();
                        _finished.Dispose();
                    });
            }
        }


        private void PostWork(Task workingTask)
        {
            Interlocked.Increment(ref _done);

            if (workingTask.Exception != null)
            {
                Interlocked.Increment(ref _error);
            }

            StartNextTask();

            if (Interlocked.Decrement(ref _counter) == 0)
            {
                _finished.Set();
            }
        }

        private bool StartNextTask()
        {
            lock (_taskEnum)
            {
                if (_taskEnum.MoveNext())
                {
                    Interlocked.Increment(ref _counter);
                    _taskEnum
                        .Current
                        .ContinueWith(PostWork)
                        .ConfigureAwait(false);

                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
    }
}
