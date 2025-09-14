# Specify the minimum version of CMake required
cmake_minimum_required(VERSION 3.16)

# Define the project name
project(EssentiaWrapper LANGUAGES CXX)

# Set the C++ standard to C++11 (required by Essentia)
set(CMAKE_CXX_STANDARD 11)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Tell the linker to look for library files in your "lib" directory.
link_directories(${PROJECT_SOURCE_DIR}/lib)

# --- Define Source Files ---
set(WRAPPER_SOURCES
    essentia_wrapper.cpp
    essentia_bridge.cpp
)

# --- Create Shared Library ---
add_library(EssentiaWrapper SHARED ${WRAPPER_SOURCES})

# --- Include Directories ---
target_include_directories(EssentiaWrapper PRIVATE
    ${PROJECT_SOURCE_DIR}       # For essentia_wrapper.h in the root
    ${PROJECT_SOURCE_DIR}/include
    ${PROJECT_SOURCE_DIR}/include/eigen3
    ${PROJECT_SOURCE_DIR}/include/essentia
    ${PROJECT_SOURCE_DIR}/include/libavcodec
    ${PROJECT_SOURCE_DIR}/include/libavformat
    ${PROJECT_SOURCE_DIR}/include/libavutil
    ${PROJECT_SOURCE_DIR}/include/libswresample
    ${PROJECT_SOURCE_DIR}/include/taglib
    ${PROJECT_SOURCE_DIR}/include/tensorflow
    ${PROJECT_SOURCE_DIR}/include/tsl
    ${PROJECT_SOURCE_DIR}/include/utf8cpp
    ${PROJECT_SOURCE_DIR}/include/vamp
    ${PROJECT_SOURCE_DIR}/include/vamp-hostsdk
    ${PROJECT_SOURCE_DIR}/include/vamp-sdk
)


# --- Set MSVC Runtime Library to DLL ---
# This ensures your wrapper uses the same dynamic C++ runtime as the shared
# Essentia library, which is critical to resolve linker errors.
if(MSVC)
    set_target_properties(EssentiaWrapper PROPERTIES
        MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>DLL"
    )
    target_compile_options(EssentiaWrapper PRIVATE "/FI${CMAKE_CURRENT_SOURCE_DIR}/essentia_msvc_fix.h")
endif()

if(MSVC)
  add_compile_definitions(
    NOMINMAX 
    _USE_MATH_DEFINES
    _CRT_SECURE_NO_WARNINGS
    WIN32_LEAN_AND_MEAN
    _INC_TIME
  )
  add_compile_options(/MP /W4 /wd4100 /Zc:__cplusplus /wd4996)
else()
  add_compile_options(-Wall -Wextra -Wno-unused-parameter -Wno-unknown-pragmas -Wno-ignored-qualifiers)
endif()

# --- Link All Dependencies ---
target_link_libraries(EssentiaWrapper PRIVATE
    essentia
    tensorflow
    tag
    yaml
    avcodec
    avformat
    avutil
    swresample
    chromaprint
    samplerate
    fftw3f
)

# --- Add Post-Build Step to Copy Runtime Dependencies (DLLs) ---
if(WIN32)
    file(GLOB THIRD_PARTY_DLLS "${PROJECT_SOURCE_DIR}/bin/*.dll")
    if(THIRD_PARTY_DLLS)
        message(STATUS "Found third-party DLLs to copy: ${THIRD_PARTY_DLLS}")
        foreach(DLL_FILE ${THIRD_PARTY_DLLS})
            add_custom_command(
                TARGET EssentiaWrapper POST_BUILD
                COMMAND ${CMAKE_COMMAND} -E copy_if_different
                "${DLL_FILE}"
                "$<TARGET_FILE_DIR:EssentiaWrapper>"
                COMMENT "Copying runtime dependency: ${DLL_FILE}"
            )
        endforeach()
    else()
        message(WARNING "No dependency DLLs found in ${PROJECT_SOURCE_DIR}/bin.")
    endif()
endif()

# --- Set Output Directory ---
set_target_properties(EssentiaWrapper PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${PROJECT_SOURCE_DIR}/bin"
    LIBRARY_OUTPUT_DIRECTORY "${PROJECT_SOURCE_DIR}/bin"
    ARCHIVE_OUTPUT_DIRECTORY "${PROJECT_SOURCE_DIR}/lib"
)

message(STATUS "Output directory set to: ${PROJECT_SOURCE_DIR}/bin")

