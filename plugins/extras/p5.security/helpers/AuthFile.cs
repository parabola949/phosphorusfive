﻿/*
 * Phosphorus Five, copyright 2014 - 2015, Thomas Hansen, phosphorusfive@gmail.com
 * Phosphorus Five is licensed under the terms of the MIT license, see the enclosed LICENSE file for details
 */

using System;
using System.IO;
using System.Web;
using System.Linq;
using System.Security;
using System.Configuration;
using System.Security.Cryptography;
using System.Collections.Generic;
using p5.exp;
using p5.core;
using p5.exp.exceptions;

namespace p5.security.helpers
{
    /// <summary>
    ///     Class wrapping access to read and write operations to and from "auth" file
    /// </summary>
    internal static class AuthFile
    {
        // Used as delegate for modification of "auth" file
        internal delegate void ModifyAuthFileDelegate (Node authFile);

        // Used to lock access to password file
        private static object _passwordFileLocker = new object ();

        // Used to cache password file, for faster access
        private static Node _authFileContent = null;

        /*
         * Helper to retrieve "auth" file as lambda object
         */
        internal static Node GetAuthFile (ApplicationContext context)
        {
            // Making sure we lock file as we retrieve it
            lock (_passwordFileLocker) {

                // Returning auth file
                return GetAuthFileInternal (context);
            }
        }

        /*
         * Helper to modify auth file
         */
        internal static void ModifyAuthFile (ApplicationContext context, ModifyAuthFileDelegate functor)
        {
            // Making sure we lock file as we retrieve it and allows caller to modify it
            lock (_passwordFileLocker) {

                // Retrieving auth file
                Node authFileNode = GetAuthFileInternal (context);

                // Invoking callback functor
                functor (authFileNode);

                // Saves updated authFileNode
                SaveAuthFileInternal (context, authFileNode);
            }
        }
        
        /*
         * Creates a new "salt" for use with hashing of passwords
         */
        internal static string CreateNewSalt()
        {
            // Generate a random salt
            RNGCryptoServiceProvider csprng = new RNGCryptoServiceProvider ();
            byte[] salt = new byte [24];
            csprng.GetBytes (salt);
            return Convert.ToBase64String (salt);
        }

        #region [ -- Private helper methods -- ]

        /*
         * Private implementation of retrieving auth file
         */
        private static Node GetAuthFileInternal (ApplicationContext context)
        {
            // Checking if we can return cached version
            if (_authFileContent != null)
                return _authFileContent;

            // Getting path
            string pwdFilePath = GetAuthFilePath (context);

            // Checking file exist
            if (!File.Exists (pwdFilePath))
                return new Node ("").Add ("users"); // No users in system

            // Reading up passwords file
            using (TextReader reader = new StreamReader (File.OpenRead (pwdFilePath))) {

                // Returning file as lambda, making sure we decrypt it first
                _authFileContent = Utilities.Convert<Node> (context, Utilities.DecryptMarvin (context, reader.ReadToEnd ()));

                // Returning cached version
                return _authFileContent;
            }
        }

        /*
         * Private implementation of saving auth file
         */
        private static void SaveAuthFileInternal (ApplicationContext context, Node authFileNode)
        {
            // Updating cached version
            _authFileContent = authFileNode.Clone ();

            // Getting path
            string pwdFilePath = GetAuthFilePath (context);

            // Saving file
            using (TextWriter writer = new StreamWriter (File.Create (pwdFilePath))) {
            
                // Writing auth file content to disc, making sure we store it encrypted
                writer.Write (Utilities.EncryptMarvin (context, Utilities.Convert<string> (context, authFileNode.Children)));
            }
        }

        /*
         * Returns path to auth file
         */
        private static string GetAuthFilePath (ApplicationContext context)
        {
            // Getting filepath to pwd file
            string rootFolder = context.RaiseNative ("p5.core.application-folder").Get<string> (context);

            // The logic below makes it possible to store auth file OUTSIDE of main web application folder!
            string pwdFilePath = context.RaiseNative ("p5.security.get-auth-file").Get<string> (context).Replace ("~", rootFolder);

            // Returning path to caller
            return pwdFilePath;
        }

        #endregion
    }
}