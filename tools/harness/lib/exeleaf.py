#!/usr/bin/env python3
"""Run one leaf routine of the original (testme.exe, the 2.0 build) unchanged under unicorn, and read what it answers.

    from exeleaf import Exe
    x = Exe()                                  # the image mapped at its base, a stack, a heap, a HLT sentinel
    obj = x.alloc( 0x1470 )                    # zeroed memory the routine reads
    x.write( obj + 0x1464, struct.pack( '<i', 2 ) )
    eax = x.call( 0x00503ac0, 0, ecx=obj )     # stdcall/thiscall/cdecl: arguments pushed right to left, ECX given

What "leaf" means here (docs/TOOLING.md, "The executable's own routines under unicorn"): no Windows import, no thread,
no object built at run time unless the caller builds it in `alloc`'d memory. The C runtime is in the image, so
`__ftol` works. The x87 control word is 0x027F (53-bit, the Windows default) unless `fpcw` says otherwise.
`call` returns EAX as an unsigned int; `signed( v )` turns it into a signed one. `reads( fn )` lists the addresses
outside the stack a call read, to see which globals it touched.

Must run under $OPENTPW_EMU_PYTHON (unicorn, pefile). lndtile.py's Tiler carries its own copy of this
set-up; this is the one to import for a new oracle.
"""
import struct
import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
import pefile
from unicorn import Uc, UC_ARCH_X86, UC_MODE_32, UC_HOOK_MEM_READ
from unicorn.x86_const import UC_X86_REG_ESP, UC_X86_REG_ECX, UC_X86_REG_EAX, UC_X86_REG_FPCW

EXE = paths.exe()


class Exe:
	def __init__( self, exe=EXE, fpcw=0x027F ):
		pe = pefile.PE( exe, fast_load=True )
		ib = pe.OPTIONAL_HEADER.ImageBase
		mu = self.mu = Uc( UC_ARCH_X86, UC_MODE_32 )
		hi = max( ib + s.VirtualAddress + max( s.Misc_VirtualSize, s.SizeOfRawData ) for s in pe.sections )
		mu.mem_map( ib, ( ( hi + 0xfff ) & ~0xfff ) - ib )
		mu.mem_write( ib, pe.header[ :0x400 ] )
		for s in pe.sections:
			mu.mem_write( ib + s.VirtualAddress, s.get_data() )
		self.STACK = 0x30000000
		mu.mem_map( self.STACK, 0x200000 )
		self.SENT = 0x50000000
		mu.mem_map( self.SENT, 0x1000 )
		mu.mem_write( self.SENT, b'\xf4' )
		self.HEAP = self.hp = 0x60000000
		mu.mem_map( self.HEAP, 0x4000000 )
		self.fpcw = fpcw

	def alloc( self, size ):
		a = self.hp
		self.hp += ( size + 0xff ) & ~0xff
		self.mu.mem_write( a, b'\0' * size )
		return a

	def write( self, addr, data ):
		self.mu.mem_write( addr, bytes( data ) )

	def read( self, addr, n ):
		return bytes( self.mu.mem_read( addr, n ) )

	def call( self, fn, *args, ecx=0 ):
		sp = self.STACK + 0x100000
		for a in reversed( args ):
			sp -= 4
			self.mu.mem_write( sp, struct.pack( '<I', a & 0xffffffff ) )
		sp -= 4
		self.mu.mem_write( sp, struct.pack( '<I', self.SENT ) )
		self.mu.reg_write( UC_X86_REG_ESP, sp )
		self.mu.reg_write( UC_X86_REG_ECX, ecx & 0xffffffff )
		self.mu.reg_write( UC_X86_REG_FPCW, self.fpcw )
		self.mu.emu_start( fn, self.SENT )
		return self.mu.reg_read( UC_X86_REG_EAX )

	def reads( self, fn, *args, ecx=0 ):
		seen = set()
		def hook( mu, access, addr, size, value, data ):
			if not ( self.STACK <= addr < self.STACK + 0x200000 ):
				seen.add( addr )
		h = self.mu.hook_add( UC_HOOK_MEM_READ, hook )
		try:
			self.call( fn, *args, ecx=ecx )
		finally:
			self.mu.hook_del( h )
		return sorted( seen )


def signed( v ):
	return v - ( 1 << 32 ) if v & 0x80000000 else v
