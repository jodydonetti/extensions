﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Shared.Pools;

namespace Microsoft.Extensions.Diagnostics.ResourceMonitoring.Linux;

/// <remarks>
/// We are reading files from /proc and /cgroup. Those files are dynamically generated by kernel, when the access to them is requested.
/// Those files then, are stored entirely in RAM; it is called Virtual File System (VFS). The access to the files is done by syscall that is non blocking.
/// Thus, this API can synchronous without performance loss.
/// </remarks>
internal sealed class OSFileSystem : IFileSystem
{
    public bool Exists(FileInfo fileInfo)
    {
        return fileInfo.Exists;
    }

    public string[] GetDirectoryNames(string directory, string pattern)
    {
        return Directory.GetDirectories(directory, pattern)
                .ToArray();
    }

    public int Read(FileInfo file, int length, Span<char> destination)
    {
        using var stream = file.OpenRead();
        using var rentedBuffer = new RentedSpan<byte>(length);

        var read = stream.Read(rentedBuffer.Span);

        return Encoding.ASCII.GetChars(rentedBuffer.Span.Slice(0, read), destination);
    }

    public void ReadFirstLine(FileInfo file, BufferWriter<char> destination)
        => ReadUntilTerminatorOrEnd(file, destination, (byte)'\n');

    public void ReadAll(FileInfo file, BufferWriter<char> destination)
        => ReadUntilTerminatorOrEnd(file, destination, null);

    [SkipLocalsInit]
    private static void ReadUntilTerminatorOrEnd(FileInfo file, BufferWriter<char> destination, byte? terminator)
    {
        const int MaxStackalloc = 256;

        using var stream = file.OpenRead();

        Span<byte> buffer = stackalloc byte[MaxStackalloc];
        var read = stream.Read(buffer);

        while (read != 0)
        {
            var end = 0;

            for (end = 0; end < read; end++)
            {
                if (buffer[end] == terminator)
                {
                    _ = Encoding.ASCII.GetChars(buffer.Slice(0, end), destination.GetSpan(end));
                    destination.Advance(end);

                    return;
                }
            }

            _ = Encoding.ASCII.GetChars(buffer.Slice(0, end), destination.GetSpan(end));
            destination.Advance(end);
            read = stream.Read(buffer);
        }
    }
}
