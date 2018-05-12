#import <Metal/Metal.h>
#import "IUnityGraphics.h"
#import "IUnityGraphicsMetal.h"
#import "UnityAppController.h"

//#define USE_MANAGED_STORAGE

#pragma mark Device interface retrieval

static IUnityInterfaces *s_interfaces;
static IUnityGraphicsMetal *s_graphics;

static id <MTLDevice> GetMetalDevice()
{
    if (!s_graphics) s_graphics = UNITY_GET_INTERFACE(s_interfaces, IUnityGraphicsMetal);
    return s_graphics ? s_graphics->MetalDevice() : nil;
}

void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces *interfaces)
{
    s_interfaces = interfaces;
}

void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API UnityPluginUnload(void)
{
    s_interfaces = NULL;
    s_graphics = NULL;
}

#pragma mark App controller subclass

@interface MyAppController : UnityAppController
{
}
- (void)shouldAttachRenderDelegate;
@end

@implementation MyAppController
- (void)shouldAttachRenderDelegate;
{
    UnityRegisterRenderingPluginV5(&UnityPluginLoad, &UnityPluginUnload);
}
@end

IMPL_APP_CONTROLLER_SUBCLASS(MyAppController);

#pragma mark Buffer management

void *BufferAccessor_Create(uint32_t size)
{
#ifdef USE_MANAGED_STORAGE
    id <MTLBuffer> buffer = [GetMetalDevice() newBufferWithLength:size options:MTLResourceStorageModeManaged];
#else
    id <MTLBuffer> buffer = [GetMetalDevice() newBufferWithLength:size options:MTLResourceStorageModeShared];
#endif
    return (__bridge_retained void*)buffer;
}

void BufferAccessor_Destroy(void *ptr_buffer)
{
    id <MTLBuffer> buffer = (__bridge_transfer id <MTLBuffer>)ptr_buffer;
    (void)buffer; // Just touching it; Will be automatically released.
}

void* BufferAccessor_GetContents(void *ptr_buffer)
{
    id <MTLBuffer> buffer = (__bridge id <MTLBuffer>)ptr_buffer;
    return buffer.contents;
}

#pragma mark CopyBuffer event callback

typedef struct
{
    void* source;
    void* destination;
    uint32_t length;
}
CopyBufferArgs;

static void CopyBufferCallback(int evendID, void *data)
{
    if (GetMetalDevice() == nil) return;
    if (data == NULL) return;

    CopyBufferArgs *args = data;
    if (args->source == NULL || args->destination == NULL) return;
    
    id <MTLBuffer> source = (__bridge id <MTLBuffer>)args->source;
    id <MTLBuffer> destination = (__bridge id <MTLBuffer>)args->destination;
    
    s_graphics->EndCurrentCommandEncoder();
    
    id <MTLBlitCommandEncoder> blit = [s_graphics->CurrentCommandBuffer() blitCommandEncoder];
    [blit copyFromBuffer:source sourceOffset:0 toBuffer:destination destinationOffset:0 size:args->length];
#ifdef USE_MANAGED_STORAGE
    [blit synchronizeResource:destination];
#endif
    [blit endEncoding];
}

UnityRenderingEventAndData UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API BufferAccessor_GetCopyBufferCallback()
{
    return CopyBufferCallback;
}
