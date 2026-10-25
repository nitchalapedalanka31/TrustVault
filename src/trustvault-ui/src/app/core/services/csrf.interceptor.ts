import { HttpInterceptorFn } from '@angular/common/http';

export const csrfInterceptor: HttpInterceptorFn = (req, next) => {
  let headers = req.headers;

  // Extract XSRF-TOKEN cookie value if available
  const match = document.cookie.match(new RegExp('(^| )XSRF-TOKEN=([^;]+)'));
  if (match && match[2]) {
    headers = headers.set('X-XSRF-TOKEN', decodeURIComponent(match[2]));
  }

  const cloned = req.clone({
    withCredentials: true,
    headers: headers
  });

  return next(cloned);
};
