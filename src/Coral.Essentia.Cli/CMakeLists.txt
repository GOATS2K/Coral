cmake_minimum_required(VERSION 3.15)
project(CoralEssentiaCli LANGUAGES CXX)

# Set C++17 standard
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Add the executable
add_executable(Coral.Essentia.Cli Coral.Essentia.Cli.cpp)

# Platform-specific configurations
if(WIN32)
    # Windows: Use vendored libraries in lib/
    set(CMAKE_PREFIX_PATH "${CMAKE_CURRENT_SOURCE_DIR}/lib/cmake" ${CMAKE_PREFIX_PATH})

    # Include directories
    target_include_directories(Coral.Essentia.Cli PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/include
        ${CMAKE_CURRENT_SOURCE_DIR}/include/eigen3
    )

    # Preprocessor definitions for Windows
    target_compile_definitions(Coral.Essentia.Cli PRIVATE
        NOMINMAX
        _USE_MATH_DEFINES
        ESSENTIA_EXPORTS=0
        _CONSOLE
        UNICODE
        _UNICODE
    )

    # Find Essentia (has CMake config)
    find_package(Essentia REQUIRED)

    # Link Essentia
    target_link_libraries(Coral.Essentia.Cli PRIVATE Essentia::essentia)

    # Find and link other libraries manually
    set(LIB_DIR "${CMAKE_CURRENT_SOURCE_DIR}/lib")

    target_link_libraries(Coral.Essentia.Cli PRIVATE
        ${LIB_DIR}/avcodec.lib
        ${LIB_DIR}/avformat.lib
        ${LIB_DIR}/avutil.lib
        ${LIB_DIR}/swresample.lib
        ${LIB_DIR}/chromaprint.lib
        ${LIB_DIR}/fftw3f.lib
        ${LIB_DIR}/samplerate.lib
        ${LIB_DIR}/tag.lib
        ${LIB_DIR}/tensorflow.lib
        ${LIB_DIR}/yaml.lib
        ${LIB_DIR}/zlibd.lib
    )

    # Copy DLLs to output directory after build
    add_custom_command(TARGET Coral.Essentia.Cli POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_directory
        ${CMAKE_CURRENT_SOURCE_DIR}/bin
        $<TARGET_FILE_DIR:Coral.Essentia.Cli>
        COMMENT "Copying DLLs to output directory..."
    )

else()
    # Linux/macOS: Use system libraries + vendored Essentia

    # Try to find Essentia from local lib/ first, then system
    set(CMAKE_PREFIX_PATH "${CMAKE_CURRENT_SOURCE_DIR}/lib/cmake" ${CMAKE_PREFIX_PATH})

    # Find Essentia
    find_package(Essentia REQUIRED)

    # Include Eigen3 (header-only)
    find_path(EIGEN3_INCLUDE_DIR
        NAMES Eigen/Core
        PATHS
            ${CMAKE_CURRENT_SOURCE_DIR}/include/eigen3
            /usr/include/eigen3
            /usr/local/include/eigen3
            /opt/homebrew/include/eigen3
    )

    if(EIGEN3_INCLUDE_DIR)
        target_include_directories(Coral.Essentia.Cli PRIVATE ${EIGEN3_INCLUDE_DIR})
    endif()

    # Include Essentia headers
    target_include_directories(Coral.Essentia.Cli PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/include
    )

    # Link Essentia
    target_link_libraries(Coral.Essentia.Cli PRIVATE Essentia::essentia)

    # Find system libraries using pkg-config or find_library
    find_package(PkgConfig QUIET)

    # FFmpeg libraries
    if(PKG_CONFIG_FOUND)
        pkg_check_modules(LIBAVCODEC REQUIRED libavcodec)
        pkg_check_modules(LIBAVFORMAT REQUIRED libavformat)
        pkg_check_modules(LIBAVUTIL REQUIRED libavutil)
        pkg_check_modules(LIBSWRESAMPLE REQUIRED libswresample)
        pkg_check_modules(FFTW3F REQUIRED fftw3f)
        pkg_check_modules(SAMPLERATE REQUIRED samplerate)
        pkg_check_modules(TAGLIB REQUIRED taglib)
        pkg_check_modules(CHROMAPRINT REQUIRED libchromaprint)
        pkg_check_modules(YAML REQUIRED yaml-0.1)

        target_link_libraries(Coral.Essentia.Cli PRIVATE
            ${LIBAVCODEC_LIBRARIES}
            ${LIBAVFORMAT_LIBRARIES}
            ${LIBAVUTIL_LIBRARIES}
            ${LIBSWRESAMPLE_LIBRARIES}
            ${FFTW3F_LIBRARIES}
            ${SAMPLERATE_LIBRARIES}
            ${TAGLIB_LIBRARIES}
            ${CHROMAPRINT_LIBRARIES}
            ${YAML_LIBRARIES}
        )

        target_include_directories(Coral.Essentia.Cli PRIVATE
            ${LIBAVCODEC_INCLUDE_DIRS}
            ${LIBAVFORMAT_INCLUDE_DIRS}
            ${LIBAVUTIL_INCLUDE_DIRS}
            ${LIBSWRESAMPLE_INCLUDE_DIRS}
        )
    else()
        # Fallback: try to find libraries manually
        message(WARNING "pkg-config not found, falling back to find_library")

        find_library(AVCODEC_LIB avcodec REQUIRED)
        find_library(AVFORMAT_LIB avformat REQUIRED)
        find_library(AVUTIL_LIB avutil REQUIRED)
        find_library(SWRESAMPLE_LIB swresample REQUIRED)
        find_library(FFTW3F_LIB fftw3f REQUIRED)
        find_library(SAMPLERATE_LIB samplerate REQUIRED)
        find_library(TAG_LIB tag REQUIRED)
        find_library(CHROMAPRINT_LIB chromaprint REQUIRED)
        find_library(YAML_LIB yaml REQUIRED)

        target_link_libraries(Coral.Essentia.Cli PRIVATE
            ${AVCODEC_LIB}
            ${AVFORMAT_LIB}
            ${AVUTIL_LIB}
            ${SWRESAMPLE_LIB}
            ${FFTW3F_LIB}
            ${SAMPLERATE_LIB}
            ${TAG_LIB}
            ${CHROMAPRINT_LIB}
            ${YAML_LIB}
        )
    endif()

    # Find TensorFlow
    find_library(TENSORFLOW_LIB
        NAMES tensorflow
        PATHS
            /usr/local/lib
            /opt/homebrew/lib
            ${CMAKE_CURRENT_SOURCE_DIR}/lib
    )

    if(TENSORFLOW_LIB)
        target_link_libraries(Coral.Essentia.Cli PRIVATE ${TENSORFLOW_LIB})
    else()
        message(WARNING "TensorFlow library not found. Make sure it's installed.")
    endif()

    # Set RPATH for finding shared libraries at runtime
    if(APPLE)
        set_target_properties(Coral.Essentia.Cli PROPERTIES
            BUILD_RPATH "${CMAKE_CURRENT_SOURCE_DIR}/bin;/usr/local/lib;/opt/homebrew/lib"
            INSTALL_RPATH "@executable_path;@executable_path/../lib;/usr/local/lib;/opt/homebrew/lib"
        )
    else()
        # Linux
        set_target_properties(Coral.Essentia.Cli PROPERTIES
            BUILD_RPATH "${CMAKE_CURRENT_SOURCE_DIR}/bin:/usr/local/lib"
            INSTALL_RPATH "$ORIGIN:$ORIGIN/../lib:/usr/local/lib"
        )
    endif()

    # Copy shared libraries from bin/ if they exist (for vendored Essentia)
    if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/bin")
        add_custom_command(TARGET Coral.Essentia.Cli POST_BUILD
            COMMAND ${CMAKE_COMMAND} -E copy_directory
            ${CMAKE_CURRENT_SOURCE_DIR}/bin
            $<TARGET_FILE_DIR:Coral.Essentia.Cli>
            COMMENT "Copying shared libraries to output directory..."
        )
    endif()

    # Preprocessor definitions for Unix
    target_compile_definitions(Coral.Essentia.Cli PRIVATE
        _USE_MATH_DEFINES
        ESSENTIA_EXPORTS=0
    )
endif()

# Enable warnings
if(MSVC)
    target_compile_options(Coral.Essentia.Cli PRIVATE /W3)
else()
    target_compile_options(Coral.Essentia.Cli PRIVATE -Wall -Wextra)
endif()

# Output build information
message(STATUS "Building Coral.Essentia.Cli for ${CMAKE_SYSTEM_NAME}")
message(STATUS "C++ Standard: ${CMAKE_CXX_STANDARD}")
message(STATUS "Build Type: ${CMAKE_BUILD_TYPE}")
