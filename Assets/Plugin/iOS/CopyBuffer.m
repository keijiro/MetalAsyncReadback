#import <Metal/Metal.h>
#import <TargetConditionals.h>
#import "Unity/IUnityGraphics.h"
#import "Unity/IUnityGraphicsMetal.h"

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

#pragma mark - CopyBuffer event callback

typedef struct
{
    void* source;       // id <MTLBuffer>
    void* destination;  // byte*
    int32_t length;
}
CopyBufferEventArgs;

static dispatch_group_t s_taskGroup;

static void CopyBufferEventCallback(int evendID, void *data)
{
    if (GetMetalDevice() == nil || data == NULL) return;

    CopyBufferEventArgs args = *(CopyBufferEventArgs*)data;
    if (args.source == NULL || args.destination == NULL) return;

    id <MTLBuffer> source = (__bridge id <MTLBuffer>)args.source;

#if TARGET_OS_OSX
    // Synchronize the managed buffer in case of macOS.
    s_graphics->EndCurrentCommandEncoder();
    id <MTLBlitCommandEncoder> blit = [s_graphics->CurrentCommandBuffer() blitCommandEncoder];
    [blit synchronizeResource:source];
    [blit endEncoding];
#endif

    // Dispatch group for background memcpy operations.
    if (s_taskGroup == NULL) s_taskGroup = dispatch_group_create();

    // Add command completion handler that kicks in the async copy block.
    [s_graphics->CurrentCommandBuffer() addCompletedHandler:^(id<MTLCommandBuffer> _Nonnull cb) {
        dispatch_group_async(s_taskGroup, dispatch_get_global_queue(QOS_CLASS_USER_INTERACTIVE, 0), ^{
            // NOTE: Compute buffers managed by Unity have a single int counter at the head of the buffer.
            // To skip this part, a 4-byte offset is added to the sourceOffset argument.
            // Also we use the first 4 bytes of the destination buffer to store the result.
            memcpy(args.destination + 4, source.contents + 4, args.length);
            *(uint32_t *)args.destination = args.length;
        });
    }];
}

UnityRenderingEventAndData UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API CopyBuffer_GetCallback()
{
    return CopyBufferEventCallback;
}

void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API CopyBuffer_WaitTasks()
{
    if (s_taskGroup != NULL) dispatch_group_wait(s_taskGroup, DISPATCH_TIME_FOREVER);
}
