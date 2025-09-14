// --- Precompiled Header (PCH) for MSVC ---
// This file acts as a "firewall" to ensure that all necessary standard
// library headers are compiled correctly in a clean environment BEFORE any
// conflicting Essentia headers are processed.

#pragma once

// This is the most critical fix. One of Essentia's headers is using a
// compiler attribute that MSVC does not recognize. Defining it as empty
// tells the compiler to ignore it, which prevents a cascade of errors.
#define __declspec(constant)

// --- Standard Library Includes ---
// By including these here, we guarantee they are parsed correctly.
#include <vector>
#include <string>
#include <iostream>
#include <stdexcept>
#include <algorithm>
#include <time.h> // Use the C header to definitively resolve the <ctime> conflict

