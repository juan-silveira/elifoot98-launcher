cmake_minimum_required(VERSION 3.15)
project(ScreenHook C CXX)

# Alvo: DLL x86 pra injetar em otvdmw.exe (que eh 32-bit).
# Build no CI com:
#   cmake -A Win32 -S native/ScreenHook -B build/screenhook
#   cmake --build build/screenhook --config Release

set(CMAKE_C_STANDARD 99)
set(CMAKE_CXX_STANDARD 14)

# Runtime estatico pra nao depender de VC++ Redistributable
if (MSVC)
    foreach(flag_var
        CMAKE_C_FLAGS CMAKE_C_FLAGS_DEBUG CMAKE_C_FLAGS_RELEASE
        CMAKE_CXX_FLAGS CMAKE_CXX_FLAGS_DEBUG CMAKE_CXX_FLAGS_RELEASE)
        string(REGEX REPLACE "/MD" "/MT" ${flag_var} "${${flag_var}}")
    endforeach()
endif()

set(MINHOOK_SRC
    minhook/src/buffer.c
    minhook/src/hook.c
    minhook/src/trampoline.c
    minhook/src/hde/hde32.c
)

add_library(ScreenHook SHARED
    dllmain.cpp
    ${MINHOOK_SRC}
)

target_include_directories(ScreenHook PRIVATE
    minhook/include
    minhook/src
)

target_link_libraries(ScreenHook PRIVATE gdi32 user32)
set_target_properties(ScreenHook PROPERTIES
    OUTPUT_NAME "ScreenHook"
    PREFIX ""
)
