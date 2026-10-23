#ifndef _GNU_SOURCE
#define _GNU_SOURCE
#endif
#include <dlfcn.h>
#include <pthread.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

typedef void *(*resolve_fn)(void *, const char *);
typedef int (*signal_fn)(void *, const char *, const char *);
typedef int (*args_fn)(void *, void *, int, ...);
static signal_fn real_signal;
static args_fn real_args;
static pthread_mutex_t mutex = PTHREAD_MUTEX_INITIALIZER;
static int active;
static char *commits[64];
static size_t lengths[64], head, count, bytes;
static const size_t byte_limit = 65535;

static void diagnostic(const char *event, const char *interface, int value) {
    const char *path = getenv("AGRIBOT_IME_TRACE_PATH");
    if (!path) return;
    FILE *file = fopen(path, "a");
    if (file) { fprintf(file, "%s interface=%s value=%d\n", event, interface ? interface : "", value); fclose(file); }
}

int agribot_ime_available(void) { return real_signal != NULL && real_args != NULL; }

void agribot_ime_set_active(int value) {
    pthread_mutex_lock(&mutex);
    if (active != (value != 0)) diagnostic("focus", NULL, value);
    active = value != 0;
    if (!active) {
        while (count) {
            free(commits[head]);
            head = (head + 1) % 64;
            --count;
        }
        bytes = 0;
    }
    pthread_mutex_unlock(&mutex);
}

static int enqueue(const char *text) {
    size_t length = strlen(text);
    if (!length || length > byte_limit) return 0;
    pthread_mutex_lock(&mutex);
    if (!active || count == 64 || bytes + length > byte_limit) {
        pthread_mutex_unlock(&mutex);
        return 0;
    }
    char *copy = malloc(length + 1);
    if (!copy) { pthread_mutex_unlock(&mutex); return 0; }
    memcpy(copy, text, length + 1);
    size_t tail = (head + count) % 64;
    commits[tail] = copy;
    lengths[tail] = length;
    bytes += length;
    ++count;
    pthread_mutex_unlock(&mutex);
    return 1;
}

int agribot_ime_pop(char *buffer, int capacity) {
    pthread_mutex_lock(&mutex);
    if (!count) { pthread_mutex_unlock(&mutex); return 0; }
    size_t length = lengths[head];
    if (!buffer || capacity <= (int)length) {
        pthread_mutex_unlock(&mutex);
        return -(int)(length + 1);
    }
    memcpy(buffer, commits[head], length + 1);
    free(commits[head]);
    head = (head + 1) % 64;
    --count;
    bytes -= length;
    pthread_mutex_unlock(&mutex);
    return (int)length;
}

int dbus_message_is_signal(void *message, const char *interface, const char *member) {
    if (!real_signal) {
        resolve_fn resolve = (resolve_fn)dlvsym(RTLD_NEXT, "dlsym", "GLIBC_2.2.5");
        void *handle = dlopen("libdbus-1.so.3", RTLD_LAZY | RTLD_NOLOAD);
        if (handle) {
            real_signal = (signal_fn)resolve(handle, "dbus_message_is_signal");
            real_args = (args_fn)resolve(handle, "dbus_message_get_args");
            dlclose(handle);
        }
    }
    if (!real_signal) return 0;
    int result = real_signal(message, interface, member);
    if (result && strcmp(member, "CommitString") == 0) diagnostic("commit", interface, active);
    // Only this process's focused text field consumes Fcitx commits. All other
    // D-Bus traffic and unfocused/disabled fields keep Unity's normal handling.
    if (result && real_args && (strcmp(interface, "org.fcitx.Fcitx.InputContext1") == 0 ||
        strcmp(interface, "org.fcitx.Fcitx.InputContext") == 0) &&
        strcmp(member, "CommitString") == 0) {
        const char *text = NULL;
        if (real_args(message, NULL, 's', &text, 0) && text && enqueue(text)) {
            if (getenv("AGRIBOT_IME_DIAGNOSTICS"))
                fprintf(stderr, "AGRIBOT_IME_NATIVE_COMMIT bytes=%zu\n", strlen(text));
            return 0;
        }
    }
    return result;
}

void *dlsym(void *handle, const char *name) {
    resolve_fn resolve = (resolve_fn)dlvsym(RTLD_NEXT, "dlsym", "GLIBC_2.2.5");
    void *symbol = resolve(handle, name);
    if (symbol && strcmp(name, "dbus_message_is_signal") == 0) {
        real_signal = (signal_fn)symbol;
        real_args = (args_fn)resolve(handle, "dbus_message_get_args");
        return (void *)dbus_message_is_signal;
    }
    return symbol;
}
