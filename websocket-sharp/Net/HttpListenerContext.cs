#region License
/*
 * HttpListenerContext.cs
 *
 * This code is derived from HttpListenerContext.cs (System.Net) of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
 * Copyright (c) 2012-2022 sta.blockhead
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
using System.Security.Principal;
using System.Text;
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Net
{
  /// <summary>
  /// Provides the access to the HTTP request and response objects used by
  /// the <see cref="HttpListener"/> class.
  /// </summary>
  /// <remarks>
  /// This class cannot be inherited.
  /// </remarks>
  public sealed class HttpListenerContext
  {
    #region Private Fields

    private HttpListenerWebSocketContext _websocketContext;

    #endregion

    #region Internal Constructors

    internal HttpListenerContext (HttpConnection connection)
    {
      Connection = connection;

      ErrorStatusCode = 400;
      Request = new HttpListenerRequest (this);
      Response = new HttpListenerResponse (this);
    }

    #endregion

    #region Internal Properties

    internal HttpConnection Connection { get; }

    internal string ErrorMessage { get; set; }

    internal int ErrorStatusCode { get; set; }

    internal bool HasErrorMessage {
      get {
        return ErrorMessage != null;
      }
    }

    internal HttpListener Listener { get; set; }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the HTTP request object that represents a client request.
    /// </summary>
    /// <value>
    /// A <see cref="HttpListenerRequest"/> that represents the client request.
    /// </value>
    public HttpListenerRequest Request { get; }

    /// <summary>
    /// Gets the HTTP response object used to send a response to the client.
    /// </summary>
    /// <value>
    /// A <see cref="HttpListenerResponse"/> that represents a response to
    /// the client request.
    /// </value>
    public HttpListenerResponse Response { get; }

    /// <summary>
    /// Gets the client information (identity, authentication, and security
    /// roles).
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="IPrincipal"/> instance or <see langword="null"/>
    ///   if not authenticated.
    ///   </para>
    ///   <para>
    ///   The instance describes the client.
    ///   </para>
    /// </value>
    public IPrincipal User { get; private set; }

    #endregion

    #region Private Methods

    private static string createErrorContent (
      int statusCode, string statusDescription, string message
    )
    {
      return message != null && message.Length > 0
             ? String.Format (
                 "<html><body><h1>{0} {1} ({2})</h1></body></html>",
                 statusCode,
                 statusDescription,
                 message
               )
             : String.Format (
                 "<html><body><h1>{0} {1}</h1></body></html>",
                 statusCode,
                 statusDescription
               );
    }

    #endregion

    #region Internal Methods

    internal HttpListenerWebSocketContext GetWebSocketContext (string protocol)
    {
      _websocketContext = new HttpListenerWebSocketContext (this, protocol);

      return _websocketContext;
    }

    internal void SendAuthenticationChallenge (
      AuthenticationSchemes scheme, string realm
    )
    {
      Response.StatusCode = 401;

      var chal = new AuthenticationChallenge (scheme, realm).ToString ();
      Response.Headers.InternalSet ("WWW-Authenticate", chal, true);

      Response.Close ();
    }

    internal void SendError ()
    {
      try {
        Response.StatusCode = ErrorStatusCode;
        Response.ContentType = "text/html";

        var content = createErrorContent (
                        ErrorStatusCode,
                        Response.StatusDescription,
                        ErrorMessage
                      );

        var enc = Encoding.UTF8;
        var entity = enc.GetBytes (content);

        Response.ContentEncoding = enc;
        Response.ContentLength64 = entity.LongLength;

        Response.Close (entity, true);
      }
      catch {
        Connection.Close (true);
      }
    }

    internal void SendError (int statusCode)
    {
      ErrorStatusCode = statusCode;

      SendError ();
    }

    internal void SendError (int statusCode, string message)
    {
      ErrorStatusCode = statusCode;
      ErrorMessage = message;

      SendError ();
    }

    internal bool SetUser (
      AuthenticationSchemes scheme,
      string realm,
      Func<IIdentity, NetworkCredential> credentialsFinder
    )
    {
      var user = HttpUtility.CreateUser (
                   Request.Headers["Authorization"],
                   scheme,
                   realm,
                   Request.HttpMethod,
                   credentialsFinder
                 );

      if (user == null)
        return false;

      if (!user.Identity.IsAuthenticated)
        return false;

      User = user;

      return true;
    }

    internal void Unregister ()
    {
      if (Listener == null)
        return;

      Listener.UnregisterContext (this);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Accepts a WebSocket handshake request.
    /// </summary>
    /// <returns>
    /// A <see cref="HttpListenerWebSocketContext"/> that represents
    /// the WebSocket handshake request.
    /// </returns>
    /// <param name="protocol">
    /// A <see cref="string"/> that specifies the subprotocol supported on
    /// the WebSocket connection.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="protocol"/> is empty.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="protocol"/> contains an invalid character.
    ///   </para>
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// This method has already been called.
    /// </exception>
    public HttpListenerWebSocketContext AcceptWebSocket (string protocol)
    {
      if (_websocketContext != null) {
        var msg = "The accepting is already in progress.";

        throw new InvalidOperationException (msg);
      }

      if (protocol != null) {
        if (protocol.Length == 0) {
          var msg = "An empty string.";

          throw new ArgumentException (msg, "protocol");
        }

        if (!protocol.IsToken ()) {
          var msg = "It contains an invalid character.";

          throw new ArgumentException (msg, "protocol");
        }
      }

      return GetWebSocketContext (protocol);
    }

    #endregion
  }
}
