	/* Android EGL can return non-null stubs for unsupported desktop functions.
	 * Base-vertex drawing is core only in ES 3.2. Prefer the advertised OES
	 * functions on earlier contexts; otherwise use FNA3D's attribute-offset path.
	 */
	if (renderer->useES3)
	{
		int major = 0, minor = 0;
		SDL_GL_GetAttribute(SDL_GL_CONTEXT_MAJOR_VERSION, &major);
		SDL_GL_GetAttribute(SDL_GL_CONTEXT_MINOR_VERSION, &minor);
		if (major < 3 || (major == 3 && minor < 2))
		{
			renderer->supports_ARB_draw_elements_base_vertex = 0;
			if (SDL_GL_ExtensionSupported("GL_OES_draw_elements_base_vertex"))
			{
#pragma GCC diagnostic push
#pragma GCC diagnostic ignored "-Wpedantic"
				renderer->glDrawRangeElementsBaseVertex = (glfntype_glDrawRangeElementsBaseVertex)
					SDL_GL_GetProcAddress("glDrawRangeElementsBaseVertexOES");
				renderer->glDrawElementsInstancedBaseVertex = (glfntype_glDrawElementsInstancedBaseVertex)
					SDL_GL_GetProcAddress("glDrawElementsInstancedBaseVertexOES");
#pragma GCC diagnostic pop
				renderer->supports_ARB_draw_elements_base_vertex =
					renderer->glDrawRangeElementsBaseVertex != NULL &&
					renderer->glDrawElementsInstancedBaseVertex != NULL;
			}
		}
	}
