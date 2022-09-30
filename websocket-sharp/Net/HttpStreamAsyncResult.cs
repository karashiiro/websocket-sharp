#region License
/*
 * HttpStreamAsyncResult.cs
 *
 * This code is derived from HttpStreamAsyncResult.cs (System.Net) of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
 * Copyright (c) 2012-2021 sta.blockhead
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */
#endregion

#region Authors
/*
 * Authors:
 * - Gonzalo Paniagua Javier <gonzalo@novell.com>
 */
#endregion

using System;
using System.Threading;

namespace WebSocketSharp.Net
{
  internal class HttpStreamAsyncResult : IAsyncResult
  {
    #region Private Fields

    private AsyncCallback    _callback;
    private bool             _completed;
    private object           _sync;
    private ManualResetEvent _waitHandle;

    #endregion

    #region Internal Constructors

    internal HttpStreamAsyncResult (AsyncCallback callback, object state)
    {
      _callback = callback;
      AsyncState = state;

      _sync = new object ();
    }

    #endregion

    #region Internal Properties

    internal byte[] Buffer { get; set; }

    internal int Count { get; set; }

    internal Exception Exception { get; private set; }

    internal bool HasException {
      get {
        return Exception != null;
      }
    }

    internal int Offset { get; set; }

    internal int SyncRead { get; set; }

    #endregion

    #region Public Properties

    public object AsyncState { get; }

    public WaitHandle AsyncWaitHandle {
      get {
        lock (_sync) {
          if (_waitHandle == null)
            _waitHandle = new ManualResetEvent (_completed);

          return _waitHandle;
        }
      }
    }

    public bool CompletedSynchronously {
      get {
        return SyncRead == Count;
      }
    }

    public bool IsCompleted {
      get {
        lock (_sync)
          return _completed;
      }
    }

    #endregion

    #region Internal Methods

    internal void Complete ()
    {
      lock (_sync) {
        if (_completed)
          return;

        _completed = true;

        if (_waitHandle != null)
          _waitHandle.Set ();

        if (_callback != null)
          _callback.BeginInvoke (this, ar => _callback.EndInvoke (ar), null);
      }
    }

    internal void Complete (Exception exception)
    {
      lock (_sync) {
        if (_completed)
          return;

        _completed = true;
        Exception = exception;

        if (_waitHandle != null)
          _waitHandle.Set ();

        if (_callback != null)
          _callback.BeginInvoke (this, ar => _callback.EndInvoke (ar), null);
      }
    }

    #endregion
  }
}
