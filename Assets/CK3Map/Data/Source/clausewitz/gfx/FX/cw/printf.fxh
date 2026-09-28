
RWBufferTexture PdxPrintfBuffer
{
	Ref = PdxDebugPrintf
	type = uint
}

ConstantBuffer( PdxDebugPrintf )
{
	uint _PrintfBufferSize;
	int _PrintfEnabled;
}

Code 
[[
	// This define is intentionally broken, c++ code will rename it to PDX_PRINTF_ENABLED if printf is enabled and -gfxprintf is on the commandline
	// We do this so that printf resources are not referenced by any shaders when it is disabled (even tho it might still include and use the code in this file)
	#define _PDX_PRINTF_ENABLED_
	
#ifdef PDX_PRINTF_ENABLED
	#define PRINTF_MACRO( Content ) if ( PrintfEnabled ) { Content }

#ifdef PDX_DIRECTX_11
	namespace EType
	{
		static uint Type_BufferFull = 1;
		static uint Type_StringID = 2;
		static uint Type_int = 3;
		static uint Type_uint = 4;
		static uint Type_float = 5;
	};
#else
	enum EType : uint
	{
		Type_BufferFull = 1,
		Type_StringID,
		Type_int,
		Type_uint,
		Type_float,
	};
#endif
	
	static uint WritePos = 0;
	static bool PrintfEnabled = false;
	
	// This is used to conditionally turn on printf for specific vertices/pixels etc, since you most likely do not want it for everything
	void SetPrintfEnabled( bool Enabled )
	{
		PrintfEnabled = Enabled && ( _PrintfEnabled == 1 );
	}
	
	// Allocates the amount of data needed from the buffer, return false if buffer is full
	bool AllocatePrintfWrites( uint NumWrites )
	{
		InterlockedAdd( PdxPrintfBuffer[0], NumWrites, WritePos );
		WritePos++; // Skip over counter (PdxPrintfBuffer[0])
		if ( ( WritePos + NumWrites ) > ( _PrintfBufferSize - 1 ) ) // -1 since we allocate one spare for notifying full buffer
		{
			PdxPrintfBuffer[WritePos] = EType::Type_BufferFull;
			return false;
		}
		return true;
	}

	
	struct SStringID
	{
		uint _ID;
	};
	SStringID GetStringID( uint ID )
	{
		SStringID StringID;
		StringID._ID = ID;
		return StringID;
	}
	// HLSL does not allow "a ? b : c", with non native types so we have to use this to support conditional strings 
	SStringID ConditionalString( bool Condition, SStringID ID1, SStringID ID2 )
	{
		if ( Condition )
		{
			return ID1;
		}
		else
		{
			return ID2;
		}
	}
	
	uint PrintfArgSize( SStringID ID ) { return 2; }
	void WritePrintfArg( SStringID ID )
	{
		PdxPrintfBuffer[WritePos++] = EType::Type_StringID;
		PdxPrintfBuffer[WritePos++] = ID._ID;
	}
	
	
	uint PrintfArgSize( int Value ) { return 2; }
	uint PrintfArgSize( int2 Value ) { return 3; }
	uint PrintfArgSize( int3 Value ) { return 4; }
	uint PrintfArgSize( int4 Value ) { return 5; }
	void WritePrintfArg( int Value )
	{
		PdxPrintfBuffer[WritePos++] = EType::Type_int;
		PdxPrintfBuffer[WritePos++] = Value;
	}
	void WritePrintfArg( int2 Value )
	{
		PdxPrintfBuffer[WritePos++] = EType::Type_int;
		PdxPrintfBuffer[WritePos++] = Value.x;
		PdxPrintfBuffer[WritePos++] = Value.y;
	}
	void WritePrintfArg( int3 Value )
	{
		PdxPrintfBuffer[WritePos++] = EType::Type_int;
		PdxPrintfBuffer[WritePos++] = Value.x;
		PdxPrintfBuffer[WritePos++] = Value.y;
		PdxPrintfBuffer[WritePos++] = Value.z;
	}
	void WritePrintfArg( int4 Value )
	{
		PdxPrintfBuffer[WritePos++] = EType::Type_int;
		PdxPrintfBuffer[WritePos++] = Value.x;
		PdxPrintfBuffer[WritePos++] = Value.y;
		PdxPrintfBuffer[WritePos++] = Value.z;
		PdxPrintfBuffer[WritePos++] = Value.w;
	}
	
	
	uint PrintfArgSize( uint Value ) { return 2; }
	uint PrintfArgSize( uint2 Value ) { return 3; }
	uint PrintfArgSize( uint3 Value ) { return 4; }
	uint PrintfArgSize( uint4 Value ) { return 5; }
	void WritePrintfArg( uint Value )
	{
		PdxPrintfBuffer[WritePos++] = EType::Type_uint;
		PdxPrintfBuffer[WritePos++] = Value;
	}
	void WritePrintfArg( uint2 Value )
	{
		PdxPrintfBuffer[WritePos++] = EType::Type_uint;
		PdxPrintfBuffer[WritePos++] = Value.x;
		PdxPrintfBuffer[WritePos++] = Value.y;
	}
	void WritePrintfArg( uint3 Value )
	{
		PdxPrintfBuffer[WritePos++] = EType::Type_uint;
		PdxPrintfBuffer[WritePos++] = Value.x;
		PdxPrintfBuffer[WritePos++] = Value.y;
		PdxPrintfBuffer[WritePos++] = Value.z;
	}
	void WritePrintfArg( uint4 Value )
	{
		PdxPrintfBuffer[WritePos++] = EType::Type_uint;
		PdxPrintfBuffer[WritePos++] = Value.x;
		PdxPrintfBuffer[WritePos++] = Value.y;
		PdxPrintfBuffer[WritePos++] = Value.z;
		PdxPrintfBuffer[WritePos++] = Value.w;
	}
	
	
	uint PrintfArgSize( float Value ) { return 2; }
	uint PrintfArgSize( float2 Value ) { return 3; }
	uint PrintfArgSize( float3 Value ) { return 4; }
	uint PrintfArgSize( float4 Value ) { return 5; }
	void WritePrintfArg( float Value )
	{
		PdxPrintfBuffer[WritePos++] = EType::Type_float;
		PdxPrintfBuffer[WritePos++] = asuint( Value );
	}
	void WritePrintfArg( float2 Value )
	{
		PdxPrintfBuffer[WritePos++] = EType::Type_float;
		PdxPrintfBuffer[WritePos++] = asuint( Value.x );
		PdxPrintfBuffer[WritePos++] = asuint( Value.y );
	}
	void WritePrintfArg( float3 Value )
	{
		PdxPrintfBuffer[WritePos++] = EType::Type_float;
		PdxPrintfBuffer[WritePos++] = asuint( Value.x );
		PdxPrintfBuffer[WritePos++] = asuint( Value.y );
		PdxPrintfBuffer[WritePos++] = asuint( Value.z );
	}
	void WritePrintfArg( float4 Value )
	{
		PdxPrintfBuffer[WritePos++] = EType::Type_float;
		PdxPrintfBuffer[WritePos++] = asuint( Value.x );
		PdxPrintfBuffer[WritePos++] = asuint( Value.y );
		PdxPrintfBuffer[WritePos++] = asuint( Value.z );
		PdxPrintfBuffer[WritePos++] = asuint( Value.w );
	}
#endif
]]
