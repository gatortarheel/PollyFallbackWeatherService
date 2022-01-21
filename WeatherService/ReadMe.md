The **Wait and Retry** policy ```handles``` clause specifies that it becomes *active* if the HTTP response status code is anything **other** than a success code, 
i.e. not in the 200 range. It retries up to *three* times with a delay between each retry. The length of the delay before retrying starts at 1 second for the first retry, then increase to 2 seconds for the second retry, the last retry is delayed by 3 seconds.


> Hey, .NET!  Nice place ya got here. Listen, see, this here policy the boss wants is the Wait and Retry, see?  
> This  says that if the response is NOT copacetic, you, .NET, must retry **three** times, and wait an extra 
> second with each retry.  You knows, 1, 2, etc. If you, .NET, don't gots a response after the third try, then you open that box that says ```handles``` and do that.


---

The **Fallback** policy’s ```handles``` clause is the same as the Wait and Retry, the Fallback’s behavior clause becomes active if the response is anything other than a success code. The behavior clause specifies the action to take before the action is performed the onFallback delegate executes.

> Hey, .NET!  Nice looking guy, eh?  Look, this here Fallback policy - if *anything* other than a perfect response shows up, you, .NET, you open that ```handles``` box and do that.  Capiche? 

---
In this example, when the Fallback policy receives the HttpResponseMessage and sees that it is a 500, the onFallback delegate is executed. This is commonly used for logging or tracking metrics. Then the fallback action is executed; the provided example calls a method, but you can do this with a lambda expression. The method is passed three parameters including the HttpResponseMessage from the failed request to the Temperature Service.

The fallback action method returns a HttpResponseMessage that is passed back to the original caller in place of the HttpResponseMessage received from the Temperature Service. In this example the action to take is to send a dummy message to an admin, but it could be anything you want.
