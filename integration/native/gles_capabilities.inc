	/* Mobile compatibility path: avoid EGL desktop stubs and unsynchronized
	 * mapped streaming on Android GLES. Use buffer orphan/subdata uploads and
	 * FNA3D's existing vertex-attribute offset fallback instead.
	 */
	if (renderer->useES3)
	{
		renderer->supports_ARB_map_buffer_range = 0;
		renderer->supports_ARB_draw_elements_base_vertex = 0;
		FNA3D_LogInfo("ANDROID_GLES_STREAMING subdata+attribute-offsets");
	}
