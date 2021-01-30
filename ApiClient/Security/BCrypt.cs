using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace MaFi.WebShareCz.ApiClient.Security
{
    internal static class BCrypt
    {
        private static readonly string _ascii64 = "./0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        public static string HashPassword(string password, string salt)
        {
            EncodedString encodedPassword = Encoding.UTF8.GetBytes(password);
            EncodedString encodedSalt = Encoding.UTF8.GetBytes(salt);
            string md5PasswordHash = MD5HashPassword(encodedPassword, encodedSalt);
            byte[] md5PasswordHashBytes = Encoding.ASCII.GetBytes(md5PasswordHash);
            using (SHA1CryptoServiceProvider sha1Provider = new SHA1CryptoServiceProvider())
            {
                byte[] sha1PasswordHashBytes = sha1Provider.ComputeHash(md5PasswordHashBytes);
                return BitConverter.ToString(sha1PasswordHashBytes).Replace("-", "").ToLower();
            }
        }

        /// <summary>
        /// Emil's adaptation of md5crypt() from:
        /// $OpenBSD: md5crypt.c,v 1.13 2003/08/07 00:30:21 deraadt Exp $
        /// $FreeBSD: crypt.c,v 1.5 1996/10/14 08:34:02 phk Exp $
        /// Original license:
        /// ----------------------------------------------------------------------------
        /// "THE BEER-WARE LICENSE" (Revision 42):
        /// <phk @login.dknet.dk> wrote this file.As long as you retain this notice you
        /// can do whatever you want with this stuff.If we meet some day, and you think
        /// this stuff is worth it, you can buy me a beer in return.   Poul-Henning Kamp
        /// ----------------------------------------------------------------------------
        /// 
        /// The JavaScript adaptation is copyright (c) 2004 Emil Mikulic
        /// </summary>
        private static string MD5HashPassword(EncodedString password, EncodedString salt)
        {
            EncodedString ctx = password + "$1$" + salt;
            EncodedString ctx1 = GetMD5(password + salt + password);

            /* "Just as many characters of ctx1" (as there are in the password) */
            for (var pl = password.Length; pl > 0; pl -= 16)
                ctx += ctx1.Subdata(0, (pl > 16) ? 16 : pl);

            /* "Then something really weird" */
            for (int i = password.Length; i != 0; i >>= 1)
                if ((i & 1) == 1)
                    ctx += 0;
                else
                    ctx += (byte)password[0];
            ctx = GetMD5(ctx);

            /* "Just to make sure things don't run too fast" */
            for (int i = 0; i < 1000; i++)
            {
                ctx1 = "";
                if ((i & 1) == 1) ctx1 += password;
                else ctx1 += ctx;

                if ((i % 3) != 0) ctx1 += salt;

                if ((i % 7) != 0) ctx1 += password;

                if ((i & 1) != 0) ctx1 += ctx;
                else ctx1 += password;

                ctx = GetMD5(ctx1);
            }

            return new string(("$1$" + salt + "$" + 
             To64Triplet(ctx, 0, 6, 12) +
             To64Triplet(ctx, 1, 7, 13) +
             To64Triplet(ctx, 2, 8, 14) +
             To64Triplet(ctx, 3, 9, 15) +
             To64Triplet(ctx, 4, 10, 5) +
             To64Single(ctx, 11)
             ).ToCharArray());
        }

        private static byte[] GetMD5(EncodedString str)
        {
            using (MD5CryptoServiceProvider md5Provider = new MD5CryptoServiceProvider())
            {
                return md5Provider.ComputeHash(str);
            }
        }

        private static EncodedString To64Triplet(EncodedString str, int idx0, int idx1, int idx2)
        {
            int v = (str[idx0] << 16) | (str[idx1] << 8) | str[idx2];
            return To64(v, 4);
        }

        private static EncodedString To64Single(EncodedString str, int idx0)
        {
            var v = str[idx0];
            return To64(v, 2);
        }

        private static EncodedString To64(int v, int n)
        {
            EncodedString s = new byte[0];
            while (--n >= 0)
            {
                s += (byte)_ascii64[v & 0x3f];
                v >>= 6;
            }
            return s;
        }

        private sealed class EncodedString
        {
            private readonly List<byte> _list = new List<byte>();

            private EncodedString(string str)
            {
                _list = new List<byte>(str.ToCharArray().Select(c => (byte)c).ToArray());
            }

            private EncodedString(byte[] binary)
            {
                _list = new List<byte>(binary);
            }

            public int Length => _list.Count;

            public EncodedString Subdata(int startIndex, int length)
            {
                byte[] subdata = new byte[length];
                _list.CopyTo(startIndex, subdata, 0, length);
                return subdata;
            }

            public byte this[int index] => _list[index];

            public char[] ToCharArray() => _list.Select(b => (char)b).ToArray();

            public static EncodedString operator +(EncodedString a, EncodedString b) => new EncodedString(a._list.Concat(b._list).ToArray());

            public static implicit operator byte[](EncodedString str)
            {
                return str._list.ToArray();
            }

            public static implicit operator EncodedString(byte[] binary)
            {
                return new EncodedString(binary);
            }

            public static implicit operator EncodedString(byte binary)
            {
                return new EncodedString(new byte[] { binary });
            }

            public static implicit operator EncodedString(string str)
            {
                return new EncodedString(str);
            }
        }
    }
}
