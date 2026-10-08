cmake_minimum_required(VERSION 3.6)
project(MY_APP)

# Boxedwine (emulador x86 + kernel Linux + X11) compilado como a libmain.so que o
# SDLActivity carrega. Mesma selecao de fontes da build web (sem a UI do
# Boxedwine, sem OpenGL), mais o JIT nativo da arquitetura.
set(BW ${CMAKE_CURRENT_SOURCE_DIR}/../../../third_party/Boxedwine)

file(GLOB SRC_SDL ${BW}/source/sdl/*.cpp ${BW}/source/sdl/singleThreaded/*.cpp ${BW}/source/sdl/multiThreaded/*.cpp)
file(GLOB_RECURSE SRC_EMU ${BW}/source/emulation/*.cpp)
list(FILTER SRC_EMU EXCLUDE REGEX "/wasm/")
file(GLOB_RECURSE SRC_KERNEL ${BW}/source/kernel/*.cpp)
file(GLOB SRC_OUTROS
    ${BW}/platform/sdl/*.cpp
    ${BW}/platform/linux/*.cpp
    ${BW}/source/io/*.cpp
    ${BW}/source/util/*.cpp
    ${BW}/source/x11/*.cpp
    ${BW}/lib/softfloat/source/*.c
    ${BW}/lib/softfloat/source/8086-SSE/*.c)
list(FILTER SRC_OUTROS EXCLUDE REGEX "networkutils.cpp")   # so a UI usa (curl)
file(GLOB_RECURSE SRC_ASMJIT ${BW}/lib/asmjit/asmjit/*.cpp)
set(SRC_ZIP
    ${BW}/lib/zlib/contrib/minizip/ioapi.c
    ${BW}/lib/zlib/contrib/minizip/mztools.c
    ${BW}/lib/zlib/contrib/minizip/unzip.c
    ${BW}/lib/zlib/contrib/minizip/zip.c)

add_library(main SHARED semgl.cpp ${SRC_SDL} ${SRC_EMU} ${SRC_KERNEL} ${SRC_OUTROS} ${SRC_ASMJIT} ${SRC_ZIP})

target_include_directories(main PRIVATE
    ${BW}/include ${BW}/lib/asmjit ${BW}/lib/simde ${BW}/lib/zlib ${BW}/lib/zlib/contrib/minizip
    ${BW}/lib/softfloat/source/include ${BW}/lib/softfloat/source ${BW}/lib/softfloat/source/8086-SSE)

target_compile_definitions(main PRIVATE
    SDL2=1 BOXEDWINE_ZLIB BOXEDWINE_DISABLE_UI BOXEDWINE_HAS_SETJMP BOXEDWINE_POSIX
    SIMDE_SSE2_NO_NATIVE ASMJIT_STATIC BOXEDWINE_64 BOXEDWINE_JIT BOXEDWINE_MULTI_THREADED
    BOXEDWINE_MEM_CACHE BOXEDWINE_HOST_EXCEPTIONS BOXEDWINE_ANDROID)

if(ANDROID_ABI STREQUAL "arm64-v8a")
    target_compile_definitions(main PRIVATE BOXEDWINE_JIT_ARMV8 SIMDE_NO_NEON SIMDE_SSE2_NO_NEON)
    list(FILTER SRC_EMU EXCLUDE REGEX "/x64/")
elseif(ANDROID_ABI STREQUAL "x86_64")
    target_compile_definitions(main PRIVATE BOXEDWINE_JIT_X64 ASMJIT_NO_AARCH64)
endif()

set_source_files_properties(semgl.cpp ${SRC_SDL} ${SRC_EMU} ${SRC_KERNEL} ${SRC_OUTROS} ${SRC_ASMJIT}
    PROPERTIES COMPILE_OPTIONS "$<$<COMPILE_LANGUAGE:CXX>:-std=c++20>")
target_compile_options(main PRIVATE -O2 -Wno-invalid-offsetof -Wno-delete-incomplete -Wno-unused-result
    -Wno-unknown-pragmas -Wno-unused-variable -Wno-unused-function -Wno-deprecated-declarations)

target_link_libraries(main SDL2 z log android dl m)
