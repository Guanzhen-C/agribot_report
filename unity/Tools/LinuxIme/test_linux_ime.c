#include <assert.h>
#include <stdarg.h>
#include "agribot_linux_ime.c"

static const char *payload;
static int is_signal(void *message, const char *interface, const char *member) {
    (void)interface; (void)member;
    return message != NULL;
}
static int get_args(void *message, void *error, int type, ...) {
    (void)message; (void)error;
    assert(type == 's');
    va_list args;
    va_start(args, type);
    *va_arg(args, const char **) = payload;
    va_end(args);
    return 1;
}
int main(void) {
    real_signal = is_signal;
    real_args = get_args;
    const char *interface = "org.fcitx.Fcitx.InputContext1";
    void *message = (void *)1;
    char buffer[65536];
    payload = "\xe4\xbd\xa0\xe5\xa5\xbd";
    assert(agribot_ime_available());
    assert(dbus_message_is_signal(message, interface, "CommitString") == 1);
    assert(agribot_ime_pop(buffer, sizeof(buffer)) == 0);
    agribot_ime_set_active(1);
    assert(dbus_message_is_signal(message, interface, "CommitString") == 0);
    assert(agribot_ime_pop(buffer, 2) == -7);
    assert(agribot_ime_pop(buffer, sizeof(buffer)) == 6);
    assert(strcmp(buffer, payload) == 0);
    assert(agribot_ime_pop(buffer, sizeof(buffer)) == 0);
    assert(dbus_message_is_signal(message, "org.fcitx.Fcitx.InputContext", "CommitString") == 0);
    assert(agribot_ime_pop(buffer, sizeof(buffer)) == 6);
    assert(dbus_message_is_signal(message, "other.interface", "CommitString") == 1);
    assert(dbus_message_is_signal(message, interface, "UpdatePreedit") == 1);
    assert(dbus_message_is_signal(NULL, interface, "CommitString") == 0);
    assert(agribot_ime_pop(buffer, sizeof(buffer)) == 0);
    for (int i = 0; i < 64; ++i) assert(dbus_message_is_signal(message, interface, "CommitString") == 0);
    assert(dbus_message_is_signal(message, interface, "CommitString") == 1);
    agribot_ime_set_active(0);
    assert(agribot_ime_pop(buffer, sizeof(buffer)) == 0);
    agribot_ime_set_active(1);
    payload = "ASCII, punctuation and \xf0\x9f\x9a\x9c";
    assert(dbus_message_is_signal(message, interface, "CommitString") == 0);
    assert(agribot_ime_pop(buffer, sizeof(buffer)) == (int)strlen(payload));
    assert(strcmp(buffer, payload) == 0);
    agribot_ime_set_active(0);
    puts("Linux IME commit, focus, bounds, queue and pass-through checks passed");
    return 0;
}
