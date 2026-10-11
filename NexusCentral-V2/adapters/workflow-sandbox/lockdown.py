"""Linux network-denied launcher for trusted built-in workers, never arbitrary code."""
import ctypes
import ctypes.util
import errno
import os
import resource
import sys
lib = ctypes.CDLL(ctypes.util.find_library('seccomp') or 'libseccomp.so.2', use_errno=True)
lib.seccomp_init.argtypes = [ctypes.c_uint32]
lib.seccomp_init.restype = ctypes.c_void_p
lib.seccomp_syscall_resolve_name.argtypes = [ctypes.c_char_p]
lib.seccomp_syscall_resolve_name.restype = ctypes.c_int
lib.seccomp_rule_add.argtypes = [ctypes.c_void_p, ctypes.c_uint32, ctypes.c_int, ctypes.c_uint]
lib.seccomp_load.argtypes = [ctypes.c_void_p]
lib.seccomp_release.argtypes = [ctypes.c_void_p]
ctx = lib.seccomp_init(0x7fff0000)
if not ctx:
    raise RuntimeError('Cannot initialize sandbox network policy')
for name in ['socket', 'socketpair', 'connect', 'bind', 'listen', 'accept', 'accept4', 'sendto', 'sendmsg', 'ptrace', 'process_vm_readv', 'process_vm_writev', 'mount', 'unshare', 'setns']:
    syscall = lib.seccomp_syscall_resolve_name(name.encode())
    if syscall >= 0 and lib.seccomp_rule_add(ctx, 0x50000 | errno.EPERM, syscall, 0) != 0:
        raise RuntimeError('Cannot apply sandbox policy')
if lib.seccomp_load(ctx) != 0:
    raise RuntimeError('Cannot enforce sandbox network policy')
lib.seccomp_release(ctx)
resource.setrlimit(resource.RLIMIT_CPU, (20, 20))
resource.setrlimit(resource.RLIMIT_FSIZE, (4 * 1024 * 1024, 4 * 1024 * 1024))
resource.setrlimit(resource.RLIMIT_NOFILE, (64, 64))
os.execve(sys.argv[1], sys.argv[1:], dict(os.environ))
