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

#pragma mark CopyBuffer event callback

typedef struct
{
    void* source;       // id <MTLBuffer>
    void* destination;  // byte*
    int32_t length;
}
CopyBufferArgs;

static void CopyBufferCallback(int evendID, void *data)
{
    if (GetMetalDevice() == nil) return;
    if (data == NULL) return;
    
    CopyBufferArgs args = *(CopyBufferArgs*)data;
    if (args.source == NULL || args.destination == NULL) return;
    id <MTLBuffer> source = (__bridge id <MTLBuffer>)args.source;
    
#ifdef TARGET_OS_MAC
    s_graphics->EndCurrentCommandEncoder();
    id <MTLBlitCommandEncoder> blit = [s_graphics->CurrentCommandBuffer() blitCommandEncoder];
    [blit synchronizeResource:source];
    [blit endEncoding];
#endif
    
    [s_graphics->CurrentCommandBuffer() addCompletedHandler:^(id<MTLCommandBuffer> _Nonnull cb) {
        dispatch_async(dispatch_get_global_queue(QOS_CLASS_USER_INTERACTIVE, 0), ^{
            // NOTE: Compute buffers managed by Unity have a single int counter in the head of the buffer.
            // To skip this part, a 4-byte offset is added to the sourceOffset argument.
//            void * dummy = malloc(args.length);
            NSDate *start = NSDate.date;
            memcpy(args.destination + 4, source.contents + 4, args.length);
//                     memcpy(dummy, source.contents + 4, args.length);
            NSDate *end = NSDate.date;
//            free(dummy);
            NSTimeInterval time = [end timeIntervalSinceDate:start];
            NSLog(@"executionTime = %f", time);
            *(uint32_t *)args.destination = 1;
        });
    }];
}

UnityRenderingEventAndData UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API BufferAccessor_GetCopyBufferCallback()
{
    return CopyBufferCallback;
}
